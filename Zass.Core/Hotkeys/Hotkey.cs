using System.Globalization;

namespace Zass.Core.Hotkeys;

/// <summary>
/// A global capture shortcut (RF-1, RF-2): a set of modifiers plus a main key, kept
/// UI-free in <see cref="Zass.Core"/> so it can be validated, formatted and parsed
/// without WPF. The presentation layer maps a WPF key press to a virtual-key code and
/// the registration layer casts the modifiers to the Win32 <c>MOD_*</c> flags.
/// </summary>
public readonly record struct Hotkey(HotkeyModifierKeys Modifiers, uint VirtualKey)
{
    /// <summary>The product default, Ctrl+Shift+S.</summary>
    public static Hotkey Default => new(HotkeyModifierKeys.Control | HotkeyModifierKeys.Shift, 0x53);

    /// <summary>The display name of the main key, or <c>null</c> if it is unsupported.</summary>
    public string? KeyName => HotkeyKeys.NameFor(VirtualKey);

    /// <summary>
    /// A shortcut is valid when it has a known main key and either carries a modifier or
    /// is a standalone-capable key (function key / Print Screen). This stops the user
    /// from binding a bare letter that would then be swallowed globally (RF-2).
    /// </summary>
    public bool IsValid =>
        KeyName is not null &&
        (Modifiers != HotkeyModifierKeys.None || HotkeyKeys.IsStandalone(VirtualKey));

    /// <summary>Canonical display/persistence form, e.g. <c>Ctrl+Shift+S</c>.</summary>
    public override string ToString()
    {
        var parts = new List<string>(5);
        if (Modifiers.HasFlag(HotkeyModifierKeys.Control))
        {
            parts.Add("Ctrl");
        }

        if (Modifiers.HasFlag(HotkeyModifierKeys.Alt))
        {
            parts.Add("Alt");
        }

        if (Modifiers.HasFlag(HotkeyModifierKeys.Shift))
        {
            parts.Add("Shift");
        }

        if (Modifiers.HasFlag(HotkeyModifierKeys.Win))
        {
            parts.Add("Win");
        }

        parts.Add(KeyName ?? "0x" + VirtualKey.ToString("X2", CultureInfo.InvariantCulture));
        return string.Join("+", parts);
    }

    /// <summary>
    /// Parses the canonical form produced by <see cref="ToString"/> (modifier aliases
    /// "Ctrl"/"Control" and "Win"/"Windows" are accepted, case-insensitively). Returns
    /// false for empty input, an unknown key, or more than one main key.
    /// </summary>
    public static bool TryParse(string? text, out Hotkey hotkey)
    {
        hotkey = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var modifiers = HotkeyModifierKeys.None;
        uint virtualKey = 0;

        foreach (string token in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (token.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    modifiers |= HotkeyModifierKeys.Control;
                    break;
                case "alt":
                    modifiers |= HotkeyModifierKeys.Alt;
                    break;
                case "shift":
                    modifiers |= HotkeyModifierKeys.Shift;
                    break;
                case "win":
                case "windows":
                    modifiers |= HotkeyModifierKeys.Win;
                    break;
                default:
                    if (virtualKey != 0)
                    {
                        return false; // A second main key is not allowed.
                    }

                    virtualKey = ResolveKey(token);
                    if (virtualKey == 0)
                    {
                        return false;
                    }

                    break;
            }
        }

        if (virtualKey == 0)
        {
            return false;
        }

        hotkey = new Hotkey(modifiers, virtualKey);
        return true;
    }

    private static uint ResolveKey(string token)
    {
        uint code = HotkeyKeys.CodeFor(token);
        if (code != 0)
        {
            return code;
        }

        // Tolerate the "0xNN" fallback that ToString emits for an unmapped key.
        if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && uint.TryParse(token.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hex))
        {
            return hex;
        }

        return 0;
    }
}
