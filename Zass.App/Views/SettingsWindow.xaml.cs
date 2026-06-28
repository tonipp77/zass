using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Zass.App.Localization;
using Zass.App.Resources;
using Zass.App.Startup;
using Zass.Core.Export;
using Zass.Core.Settings;

namespace Zass.App.Views;

/// <summary>
/// The Settings screen (RF-18 "Settings"): UI language, default save format and JPEG
/// quality, "start with Windows", and the (fixed) capture shortcut. Changing the
/// language previews live (RF-20); all values are persisted only when Save is pressed.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly SettingsStore _store;
    private readonly LanguageOption _originalLanguage;

    private readonly ComboBoxItem _langSystem = new() { Tag = LanguageOption.System };
    private readonly ComboBoxItem _langEnglish = new() { Tag = LanguageOption.English };
    private readonly ComboBoxItem _langSpanish = new() { Tag = LanguageOption.Spanish };
    private readonly ComboBoxItem _fmtPng = new() { Tag = ImageExportFormat.Png };
    private readonly ComboBoxItem _fmtJpeg = new() { Tag = ImageExportFormat.Jpeg };

    private bool _loaded;
    private bool _committed;

    public SettingsWindow(AppSettings settings, SettingsStore store)
    {
        _settings = settings;
        _store = store;
        _originalLanguage = settings.Language;

        InitializeComponent();

        LanguageCombo.Items.Add(_langSystem);
        LanguageCombo.Items.Add(_langEnglish);
        LanguageCombo.Items.Add(_langSpanish);
        FormatCombo.Items.Add(_fmtPng);
        FormatCombo.Items.Add(_fmtJpeg);

        LanguageCombo.SelectedItem = ItemForLanguage(settings.Language);
        FormatCombo.SelectedItem = settings.DefaultFormat == ImageExportFormat.Jpeg ? _fmtJpeg : _fmtPng;
        QualitySlider.Value = settings.JpegQuality;
        QualityValue.Text = FormatValue(settings.JpegQuality);
        StartupCheck.IsChecked = settings.StartWithWindows;
        HotkeyValue.Text = settings.Hotkey;

        ApplyStrings();
        LocalizationManager.LanguageChanged += OnLanguageRefresh;

        _loaded = true;
    }

    private ComboBoxItem ItemForLanguage(LanguageOption option) => option switch
    {
        LanguageOption.English => _langEnglish,
        LanguageOption.Spanish => _langSpanish,
        _ => _langSystem,
    };

    /// <summary>(Re)applies every localized caption; safe to call on language change.</summary>
    private void ApplyStrings()
    {
        Title = Strings.SettingsTitle;
        LanguageLabel.Text = Strings.SettingsLanguage;
        FormatLabel.Text = Strings.SettingsDefaultFormat;
        QualityLabel.Text = Strings.SettingsJpegQuality;
        StartupCheck.Content = Strings.SettingsStartWithWindows;
        HotkeyLabel.Text = Strings.SettingsHotkey;
        HotkeyNote.Text = Strings.SettingsHotkeyFixedNote;
        SaveButton.Content = Strings.SettingsSave;
        CancelButton.Content = Strings.SettingsCancel;

        // Item content is refreshed in place so the current selection is preserved.
        _langSystem.Content = Strings.SettingsLanguageSystem;
        _langEnglish.Content = Strings.SettingsLanguageEnglish;
        _langSpanish.Content = Strings.SettingsLanguageSpanish;
        _fmtPng.Content = Strings.SettingsFormatPng;
        _fmtJpeg.Content = Strings.SettingsFormatJpeg;
    }

    private void OnLanguageRefresh(object? sender, EventArgs e) => ApplyStrings();

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded || LanguageCombo.SelectedItem is not ComboBoxItem { Tag: LanguageOption option })
        {
            return;
        }

        // Live preview without restarting (RF-20). Persisted only on Save.
        LocalizationManager.Apply(option);
    }

    private void OnQualityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (QualityValue is not null)
        {
            QualityValue.Text = FormatValue((int)e.NewValue);
        }
    }

    private static string FormatValue(int value) => value.ToString(CultureInfo.InvariantCulture);

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var language = (LanguageOption)((ComboBoxItem)LanguageCombo.SelectedItem).Tag;
        var format = (ImageExportFormat)((ComboBoxItem)FormatCombo.SelectedItem).Tag;

        _settings.Language = language;
        _settings.DefaultFormat = format;
        _settings.JpegQuality = (int)QualitySlider.Value;
        _settings.StartWithWindows = StartupCheck.IsChecked == true;

        try
        {
            _store.Save(_settings);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"Zass: settings save failed: {ex}");
            MessageBox.Show(this, Strings.SettingsSaveErrorMessage, Strings.SettingsTitle,
                MessageBoxButton.OK, MessageBoxImage.Error);
            return; // Keep the window open so the user can retry.
        }

        // The registry entry mirrors the persisted preference (RF-19).
        WindowsStartup.Set(_settings.StartWithWindows);
        LocalizationManager.Apply(language);

        _committed = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        // Esc cancels (these windows are modeless, so IsCancel doesn't auto-handle it).
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        LocalizationManager.LanguageChanged -= OnLanguageRefresh;

        // Cancelling (or closing) reverts any live language preview.
        if (!_committed && LocalizationManager.Current != _originalLanguage)
        {
            LocalizationManager.Apply(_originalLanguage);
        }

        base.OnClosed(e);
    }
}
