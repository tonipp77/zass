using Zass.Interop.Native;

namespace Zass.Interop;

/// <summary>Modifier flags for a global hotkey (matches Win32 MOD_* values).</summary>
[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Win = 0x0008,
    /// <summary>Prevents auto-repeat while the keys are held down.</summary>
    NoRepeat = 0x4000,
}

/// <summary>Thin wrapper over RegisterHotKey / UnregisterHotKey.</summary>
public static class HotkeyNative
{
    /// <summary>Windows message id raised when a registered hotkey fires.</summary>
    public const int WM_HOTKEY = 0x0312;

    /// <summary>Registers a hotkey against a window handle. Returns false if the combination is taken.</summary>
    public static bool Register(IntPtr hWnd, int id, HotkeyModifiers modifiers, uint virtualKey)
        => NativeMethods.RegisterHotKey(hWnd, id, (uint)modifiers, virtualKey);

    /// <summary>Unregisters a previously registered hotkey.</summary>
    public static bool Unregister(IntPtr hWnd, int id)
        => NativeMethods.UnregisterHotKey(hWnd, id);
}
