using System.Diagnostics;
using System.Windows.Interop;
using Zass.Interop;

namespace Zass.App.Hotkey;

/// <summary>
/// Registers the global capture hotkey (Ctrl+Shift+S) on a hidden message-only
/// window and raises <see cref="HotkeyPressed"/> on the UI thread when it fires.
/// </summary>
/// <remarks>
/// The architecture doc places the hotkey in Zass.Core, but RegisterHotKey needs
/// an HWND and a message pump; this is inherently a UI-host concern, so it lives
/// in Zass.App while the raw P/Invoke stays isolated in Zass.Interop.
/// </remarks>
internal sealed class HotkeyManager : IDisposable
{
    private const int HotkeyId = 1;
    private const uint VK_S = 0x53;
    private const int HWND_MESSAGE = -3;

    private readonly HwndSource _source;
    private bool _registered;
    private bool _disposed;

    public HotkeyManager()
    {
        var parameters = new HwndSourceParameters("ZassHotkeyWindow")
        {
            Width = 0,
            Height = 0,
            ParentWindow = new IntPtr(HWND_MESSAGE), // message-only window: never visible
            WindowStyle = 0,
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    /// <summary>Raised on the UI thread when the global hotkey is pressed.</summary>
    public event EventHandler? HotkeyPressed;

    /// <summary>True if the hotkey was successfully registered.</summary>
    public bool IsRegistered => _registered;

    /// <summary>
    /// Registers Ctrl+Shift+S. If the combination is already taken, logs it and
    /// returns false instead of throwing (PRD §9 — never fail silently, never crash).
    /// </summary>
    public bool Register()
    {
        if (_registered)
        {
            return true;
        }

        var modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.NoRepeat;
        _registered = HotkeyNative.Register(_source.Handle, HotkeyId, modifiers, VK_S);

        if (!_registered)
        {
            Trace.TraceWarning("Zass: failed to register global hotkey Ctrl+Shift+S (already in use?).");
        }

        return _registered;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == HotkeyNative.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_registered)
        {
            HotkeyNative.Unregister(_source.Handle, HotkeyId);
            _registered = false;
        }

        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
