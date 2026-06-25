namespace Core.Models;

/// <summary>
/// RGB color value used by AutoJS6 color matching helpers.
/// </summary>
public readonly record struct RgbColor(byte R, byte G, byte B)
{
    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";
}
