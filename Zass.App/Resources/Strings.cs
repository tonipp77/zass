using System.Globalization;
using System.Resources;

namespace Zass.App.Resources;

/// <summary>
/// Typed access to the localized UI strings. Hand-written (not designer-generated)
/// so it builds headless; reads the embedded <c>Strings[.culture].resources</c>
/// satellites via <see cref="ResourceManager"/> using the current UI culture.
/// </summary>
internal static class Strings
{
    private static readonly ResourceManager Manager =
        new("Zass.App.Resources.Strings", typeof(Strings).Assembly);

    private static string Get(string key) =>
        Manager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    public static string TrayTooltip => Get(nameof(TrayTooltip));
    public static string TrayCapture => Get(nameof(TrayCapture));
    public static string TrayExit => Get(nameof(TrayExit));
    public static string HotkeyConflictTitle => Get(nameof(HotkeyConflictTitle));
    public static string HotkeyConflictMessage => Get(nameof(HotkeyConflictMessage));
    public static string CaptureErrorMessage => Get(nameof(CaptureErrorMessage));
    public static string ToolPointer => Get(nameof(ToolPointer));
    public static string ToolText => Get(nameof(ToolText));
    public static string ToolArrow => Get(nameof(ToolArrow));
    public static string ToolRectangle => Get(nameof(ToolRectangle));
    public static string ToolFilledRectangle => Get(nameof(ToolFilledRectangle));
    public static string ToolFreehand => Get(nameof(ToolFreehand));
    public static string ColorPicker => Get(nameof(ColorPicker));
    public static string LabelHex => Get(nameof(LabelHex));
    public static string LabelThickness => Get(nameof(LabelThickness));
    public static string LabelTextSize => Get(nameof(LabelTextSize));
}
