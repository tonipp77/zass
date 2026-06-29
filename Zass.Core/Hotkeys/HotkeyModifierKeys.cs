namespace Zass.Core.Hotkeys;

/// <summary>
/// User-facing modifier keys for a global shortcut. The numeric values match the
/// Win32 <c>MOD_*</c> flags (ALT=1, CONTROL=2, SHIFT=4, WIN=8) so the registration
/// layer can cast straight across; the registration-only <c>NO_REPEAT</c> flag is
/// added at the boundary and is deliberately not part of this model.
/// </summary>
[Flags]
public enum HotkeyModifierKeys
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8,
}
