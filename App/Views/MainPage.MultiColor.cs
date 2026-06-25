using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace App.Views;

public sealed partial class MainPage
{
    private void ParseMultiColorArgsButton_Click(object sender, RoutedEventArgs e)
    {
        var target = sender as Microsoft.UI.Xaml.FrameworkElement;

        try
        {
            var request = ParseMultiColorRawArguments(MultiColorRawArgsTextBox.Text);
            MultiColorFindModeCheckBox.IsChecked = request.Mode == MultiColorSearchMode.FindInRegion;
            UpdateMultiColorModeUi();
            MultiColorAnchorXTextBox.Text = request.Mode == MultiColorSearchMode.DetectAtAnchor
                ? request.AnchorX.ToString()
                : "0";
            MultiColorAnchorYTextBox.Text = request.Mode == MultiColorSearchMode.DetectAtAnchor
                ? request.AnchorY.ToString()
                : "0";
            MultiColorFirstColorTextBox.Text = request.FirstColor;
            MultiColorOffsetsTextBox.Text = FormatOffsetsForJavaScript(request.Offsets);
            MultiColorThresholdTextBox.Text = request.Threshold.ToString();
            MultiColorRegionTextBox.Text = request.Region == null
                ? string.Empty
                : $"[{request.Region.X}, {request.Region.Y}, {request.Region.Width}, {request.Region.Height}]";

            Canvas.SetMultiColorResult(null);
            MultiColorResultTextBox.Text = "参数已解析，可以直接测试";
            ShowActionTip("多点比色参数已填充", StatusTone.Success, target, "解析成功");
        }
        catch (Exception ex)
        {
            ShowActionTip($"解析失败：{ex.Message}", StatusTone.Error, target, "解析失败");
        }
    }

    private async void TestMultiColorButton_Click(object sender, RoutedEventArgs e)
    {
        var target = sender as Microsoft.UI.Xaml.FrameworkElement;

        try
        {
            var screenshot = await ResolveScreenshotBytesAsync(target);
            if (screenshot == null)
            {
                return;
            }

            var request = BuildMultiColorRequest();
            SetStatus("正在执行多点找色/比色...", StatusTone.Info);

            var result = await _multiColorDetectionService.DetectAsync(screenshot.Value.Bytes, request);
            if (screenshot.Value.IsExternalFile)
            {
                await ShowExternalScreenshotPreviewAsync(
                    screenshot.Value.Bytes,
                    screenshot.Value.Width,
                    screenshot.Value.Height,
                    screenshot.Value.Label,
                    []);
            }

            Canvas.SetMultiColorResult(result);
            MultiColorResultTextBox.Text = BuildMultiColorSummary(result);
            _latestGeneratedCode = BuildMultiColorCode(request);
            UpdateButtonStates();

            ShowActionTip(
                result.IsMatch
                    ? $"多点检测命中：({result.AnchorX}, {result.AnchorY})"
                    : "多点检测未命中，查看失败点差值",
                result.IsMatch ? StatusTone.Success : StatusTone.Warning,
                target,
                result.IsMatch ? "检测命中" : "未命中");
        }
        catch (Exception ex)
        {
            Canvas.SetMultiColorResult(null);
            MultiColorResultTextBox.Text = $"执行失败：{ex.Message}";
            Services.LogService.Instance.Log($"[MultiColor] 执行失败: {ex.Message}");
            ShowActionTip($"多点检测失败：{ex.Message}", StatusTone.Error, target, "执行失败");
        }
    }

    private async void GenerateMultiColorCodeButton_Click(object sender, RoutedEventArgs e)
    {
        var target = sender as Microsoft.UI.Xaml.FrameworkElement;
        try
        {
            var request = BuildMultiColorRequest();
            var code = BuildMultiColorCode(request);
            _latestGeneratedCode = code;
            UpdateButtonStates();
            await ShowCodePreviewDialogAsync(
                BuildSingleCodePreviewItems(code, "多点找色/比色代码", "可直接复制到 AutoJS6 脚本中，按当前截图参数执行检测。"),
                "多点找色/比色代码");
        }
        catch (Exception ex)
        {
            ShowActionTip($"生成代码失败：{ex.Message}", StatusTone.Error, target, "生成失败");
        }
    }

    private void ClearMultiColorButton_Click(object sender, RoutedEventArgs e)
    {
        Canvas.SetMultiColorResult(null);
        MultiColorResultTextBox.Text = "尚未测试";
        ShowActionTip("多点检测标记已清空", StatusTone.Info, sender as Microsoft.UI.Xaml.FrameworkElement);
    }

    private void MultiColorFindModeCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        UpdateMultiColorModeUi();
    }

    private void UpdateMultiColorModeUi()
    {
        if (MultiColorFindModeCheckBox == null ||
            MultiColorAnchorXTextBox == null ||
            MultiColorAnchorYTextBox == null ||
            MultiColorRawArgsTextBox == null)
        {
            return;
        }

        var isFindMode = MultiColorFindModeCheckBox.IsChecked == true;
        MultiColorAnchorXTextBox.IsEnabled = !isFindMode;
        MultiColorAnchorYTextBox.IsEnabled = !isFindMode;
        MultiColorAnchorXTextBox.Opacity = isFindMode ? 0.55 : 1;
        MultiColorAnchorYTextBox.Opacity = isFindMode ? 0.55 : 1;
        MultiColorRawArgsTextBox.PlaceholderText = isFindMode
            ? "\"#35ca1f\",[[1,11,\"#2cc71c\"]],{region:[x1,y1,x2,y2],threshold:[26]}"
            : "2300,300,\"#35ca1f\",[[1,11,\"#2cc71c\"]],{threshold:16}";

        var hint = isFindMode
            ? "多点找色会在区域内搜索首点，X/Y 不参与计算"
            : "多点比色会在固定 X/Y 锚点检测颜色";
        ToolTipService.SetToolTip(MultiColorAnchorXTextBox, hint);
        ToolTipService.SetToolTip(MultiColorAnchorYTextBox, hint);
    }

    private MultiColorDetectionRequest BuildMultiColorRequest()
    {
        var mode = MultiColorFindModeCheckBox.IsChecked == true
            ? MultiColorSearchMode.FindInRegion
            : MultiColorSearchMode.DetectAtAnchor;

        return new MultiColorDetectionRequest
        {
            Mode = mode,
            AnchorX = mode == MultiColorSearchMode.DetectAtAnchor
                ? ParseRequiredInt(MultiColorAnchorXTextBox.Text, "X")
                : 0,
            AnchorY = mode == MultiColorSearchMode.DetectAtAnchor
                ? ParseRequiredInt(MultiColorAnchorYTextBox.Text, "Y")
                : 0,
            FirstColor = NormalizeColorInput(MultiColorFirstColorTextBox.Text),
            Threshold = Math.Clamp(ParseRequiredInt(MultiColorThresholdTextBox.Text, "阈值"), 0, 255),
            Region = ParseOptionalRegion(MultiColorRegionTextBox.Text),
            Offsets = ParseMultiColorOffsets(MultiColorOffsetsTextBox.Text)
        };
    }

    private static string BuildMultiColorSummary(MultiColorDetectionResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine(result.IsMatch ? "结果：命中" : "结果：未命中");
        sb.AppendLine($"锚点：{FormatNullablePoint(result.AnchorX, result.AnchorY)}");
        sb.AppendLine($"阈值：{result.Threshold}    候选首点：{result.CheckedCandidates}    耗时：{result.ElapsedMilliseconds}ms");
        if (result.Region != null)
        {
            sb.AppendLine($"区域：[{result.Region.X}, {result.Region.Y}, {result.Region.Width}, {result.Region.Height}]");
        }

        foreach (var point in result.PointChecks)
        {
            var actual = point.ActualColor ?? "越界";
            var status = point.IsMatch ? "OK" : "FAIL";
            sb.AppendLine($"{status} {point.Label}: ({point.X},{point.Y}) expected={point.ExpectedColor} actual={actual} diff={point.DeltaSum}");
        }

        return sb.ToString().TrimEnd();
    }

    private static string FormatNullablePoint(int? x, int? y)
    {
        return x.HasValue && y.HasValue ? $"({x.Value}, {y.Value})" : "-";
    }

    private static string BuildMultiColorCode(MultiColorDetectionRequest request)
    {
        var colors = FormatOffsetsForJavaScript(request.Offsets);
        var options = BuildMultiColorOptionsCode(request);
        var sb = new StringBuilder();

        sb.AppendLine("// AutoJS6 多点找色/比色");
        sb.AppendLine("// screen 建议由同一轮 captureScreen() 获取，避免重复截图。");
        sb.AppendLine("var screen = captureScreen();");
        sb.AppendLine("if (!screen) {");
        sb.AppendLine("  toast(\"截图失败\");");
        sb.AppendLine("  exit();");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine($"var colors = {colors};");

        if (request.Mode == MultiColorSearchMode.FindInRegion)
        {
            sb.AppendLine($"var point = images.findMultiColors(screen, \"{request.FirstColor}\", colors, {options});");
            sb.AppendLine("if (point) {");
            sb.AppendLine("  log(\"命中: \" + point.x + \",\" + point.y);");
            sb.AppendLine("}");
        }
        else
        {
            sb.AppendLine("var options = " + options + ";");
            sb.AppendLine("var ok = images.detectMultiColors");
            sb.AppendLine($"  ? images.detectMultiColors(screen, {request.AnchorX}, {request.AnchorY}, \"{request.FirstColor}\", colors, options)");
            sb.AppendLine($"  : images.detectsMultiColors(screen, {request.AnchorX}, {request.AnchorY}, \"{request.FirstColor}\", colors, options);");
            sb.AppendLine("if (ok) {");
            sb.AppendLine("  log(\"多点比色命中\");");
            sb.AppendLine("}");
        }

        return sb.ToString();
    }

    private static string FormatOffsetsForJavaScript(IReadOnlyList<MultiColorOffset> offsets)
    {
        if (offsets.Count == 0)
        {
            return "[]";
        }

        var parts = offsets.Select(offset => $"[{offset.Dx}, {offset.Dy}, \"{offset.Color}\"]");
        return "[" + string.Join(", ", parts) + "]";
    }

    private static string BuildMultiColorOptionsCode(MultiColorDetectionRequest request)
    {
        if (request.Region == null)
        {
            return $"{{ threshold: {request.Threshold} }}";
        }

        var r = request.Region;
        return $"{{ threshold: {request.Threshold}, region: [{r.X}, {r.Y}, {r.Width}, {r.Height}] }}";
    }
}
