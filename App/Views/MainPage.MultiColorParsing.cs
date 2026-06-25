using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using Core.Models;

namespace App.Views;

public sealed partial class MainPage
{
    private static int ParseRequiredInt(string? value, string name)
    {
        if (!int.TryParse(value?.Trim(), out var number))
        {
            throw new FormatException($"{name} 必须是整数");
        }

        return number;
    }

    private static string NormalizeColorInput(string? value)
    {
        var color = value?.Trim();
        if (string.IsNullOrWhiteSpace(color))
        {
            throw new FormatException("颜色不能为空");
        }

        return color.StartsWith("#") || color.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? color
            : "#" + color;
    }

    private static CropRegion? ParseOptionalRegion(string? value)
    {
        var text = value?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (!text.StartsWith("[", StringComparison.Ordinal))
        {
            text = "[" + text + "]";
        }

        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() < 4)
        {
            throw new FormatException("区域格式应为 [x, y, w, h] 或 [x1, y1, x2, y2]");
        }

        return CreateNormalizedRegion(
            root[0].GetInt32(),
            root[1].GetInt32(),
            root[2].GetInt32(),
            root[3].GetInt32());
    }

    private static IReadOnlyList<MultiColorOffset> ParseMultiColorOffsets(string? value)
    {
        var text = value?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        using var document = JsonDocument.Parse(text);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("相对点格式应为 [[dx, dy, \"#RRGGBB\"], ...]");
        }

        var offsets = new List<MultiColorOffset>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Array)
            {
                if (item.GetArrayLength() < 3)
                {
                    throw new FormatException("相对点数组至少需要 dx、dy、color");
                }

                offsets.Add(new MultiColorOffset
                {
                    Dx = item[0].GetInt32(),
                    Dy = item[1].GetInt32(),
                    Color = NormalizeColorInput(item[2].GetString())
                });
                continue;
            }

            if (item.ValueKind == JsonValueKind.Object)
            {
                offsets.Add(new MultiColorOffset
                {
                    Dx = item.GetProperty("dx").GetInt32(),
                    Dy = item.GetProperty("dy").GetInt32(),
                    Color = NormalizeColorInput(item.GetProperty("color").GetString())
                });
                continue;
            }

            throw new FormatException("相对点仅支持数组或对象格式");
        }

        return offsets;
    }

    private static MultiColorDetectionRequest ParseMultiColorRawArguments(string? value)
    {
        var text = NormalizeRawMultiColorArguments(value);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new FormatException("请先粘贴多点比色参数");
        }

        using var document = JsonDocument.Parse("[" + text + "]");
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
        {
            throw new FormatException("参数格式应为 x,y,\"#RRGGBB\",[[dx,dy,\"#RRGGBB\"],...] 或 \"#RRGGBB\",[[dx,dy,\"#RRGGBB\"],...]");
        }

        var threshold = 16;
        CropRegion? region = null;
        var isDetectAtAnchor = IsIntegerElement(root[0]);
        var colorIndex = isDetectAtAnchor ? 2 : 0;
        var offsetsIndex = isDetectAtAnchor ? 3 : 1;
        var optionalStartIndex = offsetsIndex + 1;

        if (root.GetArrayLength() <= offsetsIndex)
        {
            throw new FormatException("参数缺少首点颜色或相对点数组");
        }

        for (var i = optionalStartIndex; i < root.GetArrayLength(); i++)
        {
            ApplyMultiColorOptionalArgument(root[i], ref threshold, ref region);
        }

        return new MultiColorDetectionRequest
        {
            Mode = isDetectAtAnchor
                ? MultiColorSearchMode.DetectAtAnchor
                : MultiColorSearchMode.FindInRegion,
            AnchorX = isDetectAtAnchor ? root[0].GetInt32() : 0,
            AnchorY = isDetectAtAnchor ? root[1].GetInt32() : 0,
            FirstColor = NormalizeColorInput(root[colorIndex].GetString()),
            Offsets = ParseMultiColorOffsets(root[offsetsIndex].GetRawText()),
            Region = region,
            Threshold = Math.Clamp(threshold, 0, 255)
        };
    }

    private static string NormalizeRawMultiColorArguments(string? value)
    {
        var text = value?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var openParen = text.IndexOf('(');
        var closeParen = text.LastIndexOf(')');
        if (openParen >= 0 && closeParen > openParen)
        {
            text = text[(openParen + 1)..closeParen].Trim();
        }

        text = StripLeadingImageArgument(text);
        text = Regex.Replace(
            text,
            "([{,]\\s*)(region|threshold)\\s*:",
            "$1\"$2\":",
            RegexOptions.IgnoreCase);
        return text.Trim().TrimEnd(';').Trim();
    }

    private static string StripLeadingImageArgument(string text)
    {
        var commaIndex = FindTopLevelComma(text);
        if (commaIndex <= 0)
        {
            return text;
        }

        var firstToken = text[..commaIndex].Trim();
        var isCoordinate = int.TryParse(firstToken, out _);
        var isColor = firstToken.StartsWith("\"", StringComparison.Ordinal) ||
                      firstToken.StartsWith("#", StringComparison.Ordinal) ||
                      firstToken.StartsWith("0x", StringComparison.OrdinalIgnoreCase);

        return isCoordinate || isColor ? text : text[(commaIndex + 1)..].Trim();
    }

    private static int FindTopLevelComma(string text)
    {
        var depth = 0;
        var inString = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '"' && (i == 0 || text[i - 1] != '\\'))
            {
                inString = !inString;
                continue;
            }

            if (inString)
            {
                continue;
            }

            if (ch == '[' || ch == '{' || ch == '(')
            {
                depth++;
            }
            else if (ch == ']' || ch == '}' || ch == ')')
            {
                depth--;
            }
            else if (ch == ',' && depth == 0)
            {
                return i;
            }
        }

        return -1;
    }

    private static CropRegion ParseRegionElement(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() < 4)
        {
            throw new FormatException("region 应为 [x, y, w, h] 或 [x1, y1, x2, y2]");
        }

        return CreateNormalizedRegion(
            element[0].GetInt32(),
            element[1].GetInt32(),
            element[2].GetInt32(),
            element[3].GetInt32());
    }

    private static CropRegion CreateNormalizedRegion(int x, int y, int widthOrX2, int heightOrY2)
    {
        var width = widthOrX2;
        var height = heightOrY2;
        if (widthOrX2 > x && heightOrY2 > y)
        {
            width = widthOrX2 - x;
            height = heightOrY2 - y;
        }

        return new CropRegion
        {
            X = x,
            Y = y,
            Width = width,
            Height = height
        };
    }

    private static bool IsIntegerElement(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out _);
    }

    private static void ApplyMultiColorOptionalArgument(
        JsonElement element,
        ref int threshold,
        ref CropRegion? region)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("threshold", out var thresholdElement))
            {
                threshold = ReadThresholdElement(thresholdElement);
            }

            if (element.TryGetProperty("region", out var regionElement))
            {
                region = ParseRegionElement(regionElement);
            }

            return;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            if (element.GetArrayLength() == 1)
            {
                threshold = ReadThresholdElement(element);
                return;
            }

            region = ParseRegionElement(element);
            return;
        }

        if (element.ValueKind == JsonValueKind.Number)
        {
            threshold = element.GetInt32();
        }
    }

    private static int ReadThresholdElement(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array && element.GetArrayLength() > 0)
        {
            return element[0].GetInt32();
        }

        return element.GetInt32();
    }
}
