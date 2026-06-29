namespace Zass.Core.Hotkeys;

/// <summary>
/// Bidirectional mapping between the subset of Win32 virtual-key codes that may serve
/// as the main key of a capture shortcut and their display names. Restricting capture
/// to known keys keeps every persisted/displayed <see cref="Hotkey"/> parseable.
/// </summary>
public static class HotkeyKeys
{
    private const uint VkPrintScreen = 0x2C;
    private const uint VkF1 = 0x70;
    private const uint VkF24 = 0x87;

    private static readonly IReadOnlyDictionary<uint, string> Names = BuildNames();
    private static readonly IReadOnlyDictionary<string, uint> Codes = BuildCodes(Names);

    /// <summary>The display name for a virtual-key code, or <c>null</c> if unsupported.</summary>
    public static string? NameFor(uint virtualKey) =>
        Names.TryGetValue(virtualKey, out string? name) ? name : null;

    /// <summary>The virtual-key code for a display name (case-insensitive), or 0 if unknown.</summary>
    public static uint CodeFor(string name) =>
        Codes.TryGetValue(name, out uint code) ? code : 0;

    /// <summary>
    /// True for keys that make a reasonable shortcut on their own (function keys and
    /// Print Screen); every other key requires at least one modifier.
    /// </summary>
    public static bool IsStandalone(uint virtualKey) =>
        virtualKey == VkPrintScreen || (virtualKey >= VkF1 && virtualKey <= VkF24);

    private static Dictionary<uint, string> BuildNames()
    {
        var names = new Dictionary<uint, string>();

        for (uint vk = 0x41; vk <= 0x5A; vk++) // A–Z
        {
            names[vk] = ((char)vk).ToString();
        }

        for (uint vk = 0x30; vk <= 0x39; vk++) // 0–9
        {
            names[vk] = ((char)vk).ToString();
        }

        for (uint vk = VkF1; vk <= VkF24; vk++) // F1–F24
        {
            names[vk] = "F" + (vk - VkF1 + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        names[VkPrintScreen] = "PrintScreen";
        names[0x20] = "Space";
        names[0x2D] = "Insert";
        names[0x2E] = "Delete";
        names[0x24] = "Home";
        names[0x23] = "End";
        names[0x21] = "PageUp";
        names[0x22] = "PageDown";

        return names;
    }

    private static Dictionary<string, uint> BuildCodes(IReadOnlyDictionary<uint, string> names)
    {
        var codes = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        foreach ((uint vk, string name) in names)
        {
            codes[name] = vk;
        }

        return codes;
    }
}
