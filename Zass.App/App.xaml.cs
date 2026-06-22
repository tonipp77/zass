using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using H.NotifyIcon;
using Zass.App.Hotkey;
using Zass.App.Overlay;
using Zass.App.Resources;
using Zass.Core.Capture;

namespace Zass.App;

/// <summary>
/// Application host: lives in the system tray, owns the global hotkey, and shows
/// the capture overlay on demand. No main window.
/// </summary>
public partial class App : Application
{
    private TaskbarIcon? _trayIcon;
    private HotkeyManager? _hotkey;
    private readonly IScreenCaptureService _captureService = new ScreenCaptureService();
    private OverlayWindow? _overlay;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        CreateTrayIcon();

        _hotkey = new HotkeyManager();
        _hotkey.HotkeyPressed += (_, _) => BeginCapture();
        if (!_hotkey.Register())
        {
            MessageBox.Show(Strings.HotkeyConflictMessage, Strings.HotkeyConflictTitle,
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CreateTrayIcon()
    {
        var menu = new ContextMenu();

        var captureItem = new MenuItem { Header = Strings.TrayCapture };
        captureItem.Click += (_, _) => BeginCapture();
        menu.Items.Add(captureItem);

        menu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = Strings.TrayExit };
        exitItem.Click += (_, _) => Shutdown();
        menu.Items.Add(exitItem);

        _trayIcon = new TaskbarIcon
        {
            ToolTipText = Strings.TrayTooltip,
            IconSource = new BitmapImage(new Uri("pack://application:,,,/Resources/zass.ico")),
            ContextMenu = menu,
        };
        _trayIcon.ForceCreate();
    }

    /// <summary>
    /// Captures the active monitor and opens the overlay. Re-entrant calls (a
    /// second hotkey press while the overlay is open) are ignored.
    /// </summary>
    private void BeginCapture()
    {
        if (_overlay != null)
        {
            return;
        }

        try
        {
            CapturedImage capture = _captureService.CaptureActiveMonitor();
            _overlay = new OverlayWindow(capture);
            _overlay.Closed += (_, _) => _overlay = null;
            _overlay.Show();
        }
        catch (Exception ex)
        {
            _overlay = null;
            Trace.TraceError($"Zass: capture failed: {ex}");
            MessageBox.Show(Strings.CaptureErrorMessage, Strings.HotkeyConflictTitle,
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkey?.Dispose();
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
