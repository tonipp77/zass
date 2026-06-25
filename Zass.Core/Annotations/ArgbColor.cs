namespace Zass.Core.Annotations;

/// <summary>
/// A UI-agnostic color (8 bits per channel, straight alpha). Kept in
/// <see cref="Zass.Core"/> so the annotation model stays free of any WPF
/// dependency; the presentation layer converts it to a WPF <c>Color</c>.
/// </summary>
public readonly record struct ArgbColor(byte A, byte R, byte G, byte B)
{
    /// <summary>Opaque color from RGB channels.</summary>
    public static ArgbColor FromRgb(byte r, byte g, byte b) => new(255, r, g, b);

    public static readonly ArgbColor Red = FromRgb(255, 0, 0);
    public static readonly ArgbColor Green = FromRgb(0, 200, 0);
    public static readonly ArgbColor Blue = FromRgb(0, 120, 215);
    public static readonly ArgbColor Black = FromRgb(0, 0, 0);
    public static readonly ArgbColor White = FromRgb(255, 255, 255);
    public static readonly ArgbColor Yellow = FromRgb(255, 221, 0);
}
