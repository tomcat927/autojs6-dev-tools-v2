using System.Globalization;
using Core.Models;

namespace Core.Helpers;

public static class AutoJsColorMatcher
{
    public static RgbColor ParseColor(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Color cannot be empty.", nameof(value));
        }

        var text = value.Trim();
        if (text.StartsWith('#'))
        {
            text = text[1..];
        }
        else if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            text = text[2..];
        }

        if (text.Length == 8)
        {
            text = text[2..];
        }

        if (text.Length != 6)
        {
            throw new FormatException($"Invalid RGB color: {value}");
        }

        var r = byte.Parse(text[0..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var g = byte.Parse(text[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var b = byte.Parse(text[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return new RgbColor(r, g, b);
    }

    public static (int DeltaR, int DeltaG, int DeltaB, int DeltaSum, bool IsMatch) MatchDiff(
        RgbColor actual,
        RgbColor expected,
        int threshold)
    {
        var deltaR = Math.Abs(actual.R - expected.R);
        var deltaG = Math.Abs(actual.G - expected.G);
        var deltaB = Math.Abs(actual.B - expected.B);
        var deltaSum = deltaR + deltaG + deltaB;
        return (deltaR, deltaG, deltaB, deltaSum, deltaSum <= Math.Clamp(threshold, 0, 255));
    }
}
