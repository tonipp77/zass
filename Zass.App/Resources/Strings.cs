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

    public static string AnnotationShadow => Get(nameof(AnnotationShadow));
    public static string ArrowTriangular => Get(nameof(ArrowTriangular));
    public static string ArrowOpen => Get(nameof(ArrowOpen));
    public static string ArrowTapered => Get(nameof(ArrowTapered));
    public static string ArrowStyleLabel => Get(nameof(ArrowStyleLabel));

    public static string ToolCollage => Get(nameof(ToolCollage));
    public static string CollageTitle => Get(nameof(CollageTitle));
    public static string CollageHint => Get(nameof(CollageHint));
    public static string CollageCapture => Get(nameof(CollageCapture));
    public static string CollageRemove => Get(nameof(CollageRemove));
    public static string CollageUndo => Get(nameof(CollageUndo));
    public static string CollageRedo => Get(nameof(CollageRedo));
    public static string CollageClear => Get(nameof(CollageClear));
    public static string CollageCopy => Get(nameof(CollageCopy));
    public static string CollageSave => Get(nameof(CollageSave));
    public static string CollageHide => Get(nameof(CollageHide));
    public static string CollageZoom => Get(nameof(CollageZoom));
    public static string CollageStatus => Get(nameof(CollageStatus));
    public static string CollageCopyError => Get(nameof(CollageCopyError));
    public static string CollageAddError => Get(nameof(CollageAddError));
    public static string ToolPixelation => Get(nameof(ToolPixelation));
    public static string CollageText => Get(nameof(CollageText));
    public static string LabelPixelSize => Get(nameof(LabelPixelSize));
    public static string CollagePointer => Get(nameof(CollagePointer));
    public static string CollageCurvedArrow => Get(nameof(CollageCurvedArrow));
    public static string CollageStraightArrow => Get(nameof(CollageStraightArrow));
    public static string CollageElbowArrow => Get(nameof(CollageElbowArrow));
    public static string CollageAnnotationError => Get(nameof(CollageAnnotationError));
    public static string TrayTooltip => Get(nameof(TrayTooltip));
    public static string TrayCapture => Get(nameof(TrayCapture));
    public static string TraySettings => Get(nameof(TraySettings));
    public static string TrayAbout => Get(nameof(TrayAbout));
    public static string TrayExit => Get(nameof(TrayExit));
    public static string HotkeyConflictTitle => Get(nameof(HotkeyConflictTitle));
    public static string HotkeyConflictMessage => Get(nameof(HotkeyConflictMessage));
    public static string CaptureErrorMessage => Get(nameof(CaptureErrorMessage));
    public static string ToolPointer => Get(nameof(ToolPointer));
    public static string ToolText => Get(nameof(ToolText));
    public static string ToolLine => Get(nameof(ToolLine));
    public static string ToolArrow => Get(nameof(ToolArrow));
    public static string ToolRectangle => Get(nameof(ToolRectangle));
    public static string ToolFilledRectangle => Get(nameof(ToolFilledRectangle));
    public static string ToolFreehand => Get(nameof(ToolFreehand));
    public static string ColorPicker => Get(nameof(ColorPicker));
    public static string LabelHex => Get(nameof(LabelHex));
    public static string LabelThickness => Get(nameof(LabelThickness));
    public static string LabelTextSize => Get(nameof(LabelTextSize));
    public static string ToolCopy => Get(nameof(ToolCopy));
    public static string ToolSave => Get(nameof(ToolSave));
    public static string ToolClose => Get(nameof(ToolClose));
    public static string OverlayHint => Get(nameof(OverlayHint));
    public static string SaveDialogTitle => Get(nameof(SaveDialogTitle));
    public static string SaveFilterPng => Get(nameof(SaveFilterPng));
    public static string SaveFilterJpeg => Get(nameof(SaveFilterJpeg));
    public static string SaveErrorMessage => Get(nameof(SaveErrorMessage));
    public static string SettingsTitle => Get(nameof(SettingsTitle));
    public static string SettingsLanguage => Get(nameof(SettingsLanguage));
    public static string SettingsLanguageSystem => Get(nameof(SettingsLanguageSystem));
    public static string SettingsLanguageEnglish => Get(nameof(SettingsLanguageEnglish));
    public static string SettingsLanguageSpanish => Get(nameof(SettingsLanguageSpanish));
    public static string SettingsDefaultFormat => Get(nameof(SettingsDefaultFormat));
    public static string SettingsFormatPng => Get(nameof(SettingsFormatPng));
    public static string SettingsFormatJpeg => Get(nameof(SettingsFormatJpeg));
    public static string SettingsJpegQuality => Get(nameof(SettingsJpegQuality));
    public static string SettingsStartWithWindows => Get(nameof(SettingsStartWithWindows));
    public static string SettingsHotkey => Get(nameof(SettingsHotkey));
    public static string SettingsHotkeyHint => Get(nameof(SettingsHotkeyHint));
    public static string SettingsHotkeyReset => Get(nameof(SettingsHotkeyReset));
    public static string SettingsHotkeyInUse => Get(nameof(SettingsHotkeyInUse));
    public static string SettingsHotkeyInvalid => Get(nameof(SettingsHotkeyInvalid));
    public static string SettingsSave => Get(nameof(SettingsSave));
    public static string SettingsCancel => Get(nameof(SettingsCancel));
    public static string SettingsSaveErrorMessage => Get(nameof(SettingsSaveErrorMessage));
    public static string AboutTitle => Get(nameof(AboutTitle));
    public static string AboutDescription => Get(nameof(AboutDescription));
    public static string AboutVersionLabel => Get(nameof(AboutVersionLabel));
    public static string AboutClose => Get(nameof(AboutClose));
}
