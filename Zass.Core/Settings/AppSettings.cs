using Zass.Core.Annotations;
using Zass.Core.Export;

namespace Zass.Core.Settings;

/// <summary>
/// User preferences persisted between sessions (RF-21): UI language, default save
/// format and JPEG quality, "start with Windows", the configured capture shortcut,
/// and the last color/thickness/text size used while annotating.
/// </summary>
/// <remarks>
/// This is a plain serializable bag with public mutable properties so
/// <c>System.Text.Json</c> can round-trip it. Out-of-range or unknown values
/// (a hand-edited or corrupt file) are corrected by <see cref="Normalize"/>.
/// The color is stored as a packed 0xAARRGGBB integer to keep the JSON stable and
/// decoupled from the <see cref="ArgbColor"/> shape.
/// </remarks>
public sealed class AppSettings
{
    // Valid ranges, mirrored by the toolbar sliders and the settings screen.
    public const int MinJpegQuality = 1;
    public const int MaxJpegQuality = 100;
    public const double MinThickness = 1.0;
    public const double MaxThickness = 20.0;
    public const double MinTextSize = 8.0;
    public const double MaxTextSize = 72.0;

    public const int DefaultJpegQuality = 90;
    public const double DefaultThickness = 3.0;
    public const double DefaultTextSize = 18.0;

    /// <summary>The fixed v1 capture shortcut. Reconfiguration (RF-2) is deferred.</summary>
    public const string DefaultHotkey = "Ctrl+Shift+S";

    /// <summary>UI language preference. Defaults to following the OS language.</summary>
    public LanguageOption Language { get; set; } = LanguageOption.System;

    /// <summary>Format pre-selected in the Save dialog (RF-16).</summary>
    public ImageExportFormat DefaultFormat { get; set; } = ImageExportFormat.Png;

    /// <summary>JPEG encoder quality (1–100) used when saving as JPG.</summary>
    public int JpegQuality { get; set; } = DefaultJpegQuality;

    /// <summary>Launch Zass at Windows sign-in (RF-19). Off by default.</summary>
    public bool StartWithWindows { get; set; }

    /// <summary>The configured capture shortcut (RF-21). Fixed in v1.</summary>
    public string Hotkey { get; set; } = DefaultHotkey;

    /// <summary>Last annotation color, packed as 0xAARRGGBB.</summary>
    public uint LastColorArgb { get; set; } = PackColor(ArgbColor.Red);

    /// <summary>Last stroke thickness, in physical pixels.</summary>
    public double LastThickness { get; set; } = DefaultThickness;

    /// <summary>Last text size, in physical pixels.</summary>
    public double LastTextSize { get; set; } = DefaultTextSize;

    /// <summary>Packs an <see cref="ArgbColor"/> into a 0xAARRGGBB integer.</summary>
    public static uint PackColor(ArgbColor c) =>
        ((uint)c.A << 24) | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;

    /// <summary>Unpacks a 0xAARRGGBB integer into an <see cref="ArgbColor"/>.</summary>
    public static ArgbColor UnpackColor(uint argb) => new(
        (byte)((argb >> 24) & 0xFF),
        (byte)((argb >> 16) & 0xFF),
        (byte)((argb >> 8) & 0xFF),
        (byte)(argb & 0xFF));

    /// <summary>Convenience accessor for the last color as an <see cref="ArgbColor"/>.</summary>
    public ArgbColor LastColor
    {
        get => UnpackColor(LastColorArgb);
        set => LastColorArgb = PackColor(value);
    }

    /// <summary>
    /// Clamps numeric values to their valid ranges and resets unknown enum or empty
    /// shortcut values to their defaults. Applied after loading and before saving so a
    /// corrupt or hand-edited file can never feed invalid values into the app.
    /// </summary>
    public void Normalize()
    {
        if (!Enum.IsDefined(Language))
        {
            Language = LanguageOption.System;
        }

        if (!Enum.IsDefined(DefaultFormat))
        {
            DefaultFormat = ImageExportFormat.Png;
        }

        JpegQuality = Math.Clamp(JpegQuality, MinJpegQuality, MaxJpegQuality);
        LastThickness = Math.Clamp(LastThickness, MinThickness, MaxThickness);
        LastTextSize = Math.Clamp(LastTextSize, MinTextSize, MaxTextSize);

        if (string.IsNullOrWhiteSpace(Hotkey))
        {
            Hotkey = DefaultHotkey;
        }
    }
}
