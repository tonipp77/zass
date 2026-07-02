using Zass.Core.Hotkeys;

namespace Zass.Tests;

public class HotkeyTests
{
    [Fact]
    public void Default_IsCtrlShiftS()
    {
        Hotkey def = Hotkey.Default;

        Assert.Equal(HotkeyModifierKeys.Control | HotkeyModifierKeys.Shift, def.Modifiers);
        Assert.Equal("S", def.KeyName);
        Assert.Equal("Ctrl+Shift+S", def.ToString());
        Assert.True(def.IsValid);
    }

    [Theory]
    [InlineData(HotkeyModifierKeys.Control, 0x53, "Ctrl+S")]
    [InlineData(HotkeyModifierKeys.Control | HotkeyModifierKeys.Shift, 0x41, "Ctrl+Shift+A")]
    [InlineData(HotkeyModifierKeys.Alt, 0x70, "Alt+F1")]
    [InlineData(HotkeyModifierKeys.Control | HotkeyModifierKeys.Alt | HotkeyModifierKeys.Shift | HotkeyModifierKeys.Win, 0x31, "Ctrl+Alt+Shift+Win+1")]
    [InlineData(HotkeyModifierKeys.None, 0x2C, "PrintScreen")]
    public void ToString_UsesCanonicalModifierOrder(HotkeyModifierKeys modifiers, uint vk, string expected)
    {
        Assert.Equal(expected, new Hotkey(modifiers, vk).ToString());
    }

    [Theory]
    [InlineData("Ctrl+Shift+S")]
    [InlineData("Ctrl+Alt+Shift+Win+1")]
    [InlineData("Alt+F12")]
    [InlineData("PrintScreen")]
    public void TryParse_RoundTripsCanonicalForm(string text)
    {
        Assert.True(Hotkey.TryParse(text, out Hotkey parsed));
        Assert.Equal(text, parsed.ToString());
    }

    [Theory]
    [InlineData("control+shift+s")]
    [InlineData("CTRL+SHIFT+S")]
    [InlineData(" Ctrl + Shift + S ")]
    public void TryParse_IsCaseAndWhitespaceInsensitiveWithAliases(string text)
    {
        Assert.True(Hotkey.TryParse(text, out Hotkey parsed));
        Assert.Equal(Hotkey.Default, parsed);
    }

    [Fact]
    public void TryParse_AcceptsWindowsAlias()
    {
        Assert.True(Hotkey.TryParse("Windows+F2", out Hotkey parsed));
        Assert.Equal(HotkeyModifierKeys.Win, parsed.Modifiers);
        Assert.Equal("F2", parsed.KeyName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ctrl+Shift")]      // modifiers only, no main key
    [InlineData("Ctrl+S+A")]        // two main keys
    [InlineData("Ctrl+Nope")]       // unknown key name
    public void TryParse_RejectsInvalidInput(string text)
    {
        Assert.False(Hotkey.TryParse(text, out _));
    }

    [Fact]
    public void IsValid_RequiresAModifierForOrdinaryKeys()
    {
        Assert.False(new Hotkey(HotkeyModifierKeys.None, 0x41).IsValid); // bare "A"
        Assert.True(new Hotkey(HotkeyModifierKeys.Control, 0x41).IsValid); // "Ctrl+A"
    }

    [Theory]
    [InlineData(0x70)] // F1
    [InlineData(0x87)] // F24
    [InlineData(0x2C)] // PrintScreen
    public void IsValid_AllowsStandaloneFunctionAndPrintScreenKeys(uint vk)
    {
        Assert.True(new Hotkey(HotkeyModifierKeys.None, vk).IsValid);
    }

    [Fact]
    public void IsValid_RejectsUnknownKey()
    {
        Assert.False(new Hotkey(HotkeyModifierKeys.Control, 0x00).IsValid);
    }
}
