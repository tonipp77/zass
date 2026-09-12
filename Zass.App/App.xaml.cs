using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using H.NotifyIcon;
using Zass.App.Hotkeys;
using Zass.App.Localization;
using Zass.App.Overlay;
using Zass.App.Resources;
using Zass.App.Startup;
using Zass.App.Views;
using Zass.Core.Capture;
using Zass.Core.Hotkeys;
using Zass.Core.Settings;

namespace Zass.App;

/// <summary>
/// Application host: lives in the system tray, owns the global hotkey and the
/// persisted settings, and shows the capture overlay on demand. No main window.
/// </summary>
public partial class App : Application
{
    private readonly IScreenCaptureService _captureService = new ScreenCaptureService();
    private readonly SettingsStore _store = SettingsStore.CreateDefault();

    private TaskbarIcon? _trayIcon;
    private HotkeyManager? _hotkey;
    private OverlayWindow? _overlay;
    private SettingsWindow? _settingsWindow;
    private CollageWindow? _collageWindow;
    private bool _captureStarting;
    private AppSettings _settings = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _settings = _store.Load();
        LocalizationManager.Apply(_settings.Language);
        // Keep the Run entry in sync with the persisted preference (RF-19).
        WindowsStartup.Set(_settings.StartWithWindows);

        CreateTrayIcon();
        LocalizationManager.LanguageChanged += (_, _) => RefreshTray();

        _hotkey = new HotkeyManager();
        _hotkey.HotkeyPressed += (_, _) => BeginCapture();

        // Fall back to the default if the persisted descriptor is malformed.
        if (!Hotkey.TryParse(_settings.Hotkey, out Hotkey hotkey))
        {
            hotkey = Hotkey.Default;
            _settings.Hotkey = hotkey.ToString();
        }

        if (!_hotkey.TryApply(hotkey))
        {
            MessageBox.Show(Strings.HotkeyConflictMessage, Strings.HotkeyConflictTitle,
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CreateTrayIcon()
    {
        _trayIcon = new TaskbarIcon
        {
            IconSource = new BitmapImage(new Uri("pack://application:,,,/Resources/zass.ico")),
        };
        RefreshTray();
        _trayIcon.ForceCreate();
    }

    /// <summary>Rebuilds the tray tooltip and menu in the current language (RF-20).</summary>
    private void RefreshTray()
    {
        if (_trayIcon is null)
        {
            return;
        }

        _trayIcon.ToolTipText = Strings.TrayTooltip;

        var menu = new ContextMenu();
        menu.Items.Add(MenuItem(Strings.TrayCapture, (_, _) => BeginCapture()));
        menu.Items.Add(MenuItem(Strings.CollageTitle, (_, _) => ShowCollage()));
        menu.Items.Add(new Separator());
        menu.Items.Add(MenuItem(Strings.TraySettings, (_, _) => ShowSettings()));
        menu.Items.Add(MenuItem(Strings.TrayAbout, (_, _) => ShowAbout()));
        menu.Items.Add(new Separator());
        menu.Items.Add(MenuItem(Strings.TrayExit, (_, _) => Shutdown()));
        _trayIcon.ContextMenu = menu;
    }

    private static MenuItem MenuItem(string header, RoutedEventHandler onClick)
    {
        var item = new MenuItem { Header = header };
        item.Click += onClick;
        return item;
    }

    /// <summary>
    /// Captures the active monitor and opens the overlay. Re-entrant calls (a
    /// second hotkey press while the overlay is open) are ignored.
    /// </summary>
    private async void BeginCapture()
    {
        if (_overlay != null || _captureStarting || _collageWindow?.IsExporting == true)
        {
            return;
        }

        if (_collageWindow is not null && !_collageWindow.PrepareForCapture()) return;

        _captureStarting = true;
        bool revealCollage = _collageWindow?.IsVisible == true;
        try
        {
            if (revealCollage)
            {
                await _collageWindow!.HideForCaptureAsync();
            }
            CapturedImage capture = _captureService.CaptureActiveMonitor();
            var options = new OverlayOptions(
                _settings.LastColor,
                _settings.LastThickness,
                _settings.LastTextSize,
                _settings.DefaultFormat,
                _settings.JpegQuality);

            var overlay = new OverlayWindow(capture, options);
            overlay.CaptureExported += image => EnsureCollage().RetainCapture(image);
            overlay.AddToCollageRequested += image =>
            {
                EnsureCollage().AddCrop(image);
                revealCollage = true;
            };
            overlay.Closed += (_, _) =>
            {
                RememberAnnotationStyle(overlay);
                _overlay = null;
                if (revealCollage) _collageWindow?.Reveal();
            };
            _overlay = overlay;
            overlay.Show();
        }
        catch (Exception ex)
        {
            _overlay = null;
            Trace.TraceError($"Zass: capture failed: {ex}");
            MessageBox.Show(Strings.CaptureErrorMessage, Strings.HotkeyConflictTitle,
                MessageBoxButton.OK, MessageBoxImage.Error);
            if (revealCollage) _collageWindow?.Reveal();
        }
        finally { _captureStarting = false; }
    }

    private CollageWindow EnsureCollage()
    {
        if (_collageWindow is not null) return _collageWindow;
        var window = new CollageWindow(() => (_settings.DefaultFormat, _settings.JpegQuality));
        window.CaptureRequested += BeginCapture;
        window.Closed += (_, _) => _collageWindow = null;
        _collageWindow = window;
        return window;
    }

    private void ShowCollage()
    {
        if (_overlay is null && !_captureStarting) EnsureCollage().Reveal();
    }

    /// <summary>
    /// Persists the color/thickness/text size last used in the overlay so the next
    /// capture starts from them (RF-21). A failed write is logged, not fatal.
    /// </summary>
    private void RememberAnnotationStyle(OverlayWindow overlay)
    {
        _settings.LastColor = overlay.LastColor;
        _settings.LastThickness = overlay.LastThickness;
        _settings.LastTextSize = overlay.LastTextSize;

        try
        {
            _store.Save(_settings);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Zass: could not persist the last annotation style: {ex.Message}");
        }
    }

    private void ShowSettings()
    {
        if (_settingsWindow != null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settings, _store, hk => _hotkey!.TryApply(hk));
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void ShowAbout()
    {
        var about = new AboutWindow();
        about.Show();
        about.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _collageWindow?.CloseForShutdown();
        _hotkey?.Dispose();
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
