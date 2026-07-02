using System.Diagnostics;
using System.Windows.Interop;
using Zass.Core.Hotkeys;
using Zass.Interop;

namespace Zass.App.Hotkeys;

/// <summary>
/// Registers the configurable global capture shortcut (RF-1, RF-2) on a hidden
/// message-only window and raises <see cref="HotkeyPressed"/> on the UI thread when it
/// fires. The shortcut can be re-applied at runtime without recreating the window.
/// </summary>
/// <remarks>
/// The architecture doc places the hotkey in Zass.Core, but RegisterHotKey needs an
/// HWND and a message pump; this is inherently a UI-host concern, so it lives in
/// Zass.App while the raw P/Invoke stays isolated in Zass.Interop and the shortcut
/// model (<see cref="Hotkey"/>) stays UI-free in Zass.Core.
/// </remarks>
internal sealed class HotkeyManager : IDisposable
{
    private const int HotkeyId = 1;

    private readonly HwndSource _source;
    private Hotkey? _current;
    private bool _registered;
    private bool _disposed;

    public HotkeyManager()
    {
        var parameters = new HwndSourceParameters("ZassHotkeyWindow")
        {
            Width = 0,
            Height = 0,
            ParentWindow = new IntPtr(-3), // HWND_MESSAGE: message-only window, never visible
            WindowStyle = 0,
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    /// <summary>Raised on the UI thread when the global hotkey is pressed.</summary>
    public event EventHandler? HotkeyPressed;

    /// <summary>The shortcut currently registered, if any.</summary>
    public Hotkey? Current => _current;

    /// <summary>
    /// Applies <paramref name="hotkey"/> as the active global shortcut. On success the
    /// previous shortcut is replaced. If the new combination is invalid or already in
    /// use, the previously registered shortcut is restored and false is returned, so a
    /// failed change never leaves the app without a working shortcut (Architecture §3.2).
    /// </summary>
    public bool TryApply(Hotkey hotkey)
    {
        if (!hotkey.IsValid)
        {
            return false;
        }

        Hotkey? previous = _current;

        if (_registered)
        {
            HotkeyNative.Unregister(_source.Handle, HotkeyId);
            _registered = false;
            _current = null;
        }

        if (TryRegister(hotkey))
        {
            return true;
        }

        Trace.TraceWarning($"Zass: could not register the global hotkey '{hotkey}' (already in use?).");

        // Restore the previous shortcut so capture keeps working with the old one.
        if (previous is Hotkey prior)
        {
            TryRegister(prior);
        }

        return false;
    }

    private bool TryRegister(Hotkey hotkey)
    {
        var modifiers = (HotkeyModifiers)(uint)hotkey.Modifiers | HotkeyModifiers.NoRepeat;
        if (!HotkeyNative.Register(_source.Handle, HotkeyId, modifiers, hotkey.VirtualKey))
        {
            return false;
        }

        _current = hotkey;
        _registered = true;
        return true;
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
