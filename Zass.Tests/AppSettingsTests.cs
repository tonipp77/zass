using Zass.Core.Annotations;
using Zass.Core.Export;
using Zass.Core.Settings;

namespace Zass.Tests;

public class AppSettingsTests
{
    [Fact]
    public void Defaults_MatchTheDocumentedProductDefaults()
    {
        var settings = new AppSettings();

        Assert.Equal(LanguageOption.System, settings.Language);
        Assert.Equal(ImageExportFormat.Png, settings.DefaultFormat);
        Assert.Equal(AppSettings.DefaultJpegQuality, settings.JpegQuality);
        Assert.False(settings.StartWithWindows);
        Assert.Equal(AppSettings.DefaultHotkey, settings.Hotkey);
        Assert.Equal(ArgbColor.Red, settings.LastColor);
        Assert.Equal(AppSettings.DefaultThickness, settings.LastThickness);
        Assert.Equal(AppSettings.DefaultTextSize, settings.LastTextSize);
    }

    [Theory]
    [InlineData(255, 0, 0, 0)]
    [InlineData(255, 255, 0, 0)]
    [InlineData(128, 12, 200, 240)]
    [InlineData(0, 0, 0, 0)]
    public void Color_PacksAndUnpacksLosslessly(byte a, byte r, byte g, byte b)
    {
        var color = new ArgbColor(a, r, g, b);

        uint packed = AppSettings.PackColor(color);

        Assert.Equal(color, AppSettings.UnpackColor(packed));
    }

    [Fact]
    public void LastColor_RoundTripsThroughThePackedProperty()
    {
        var settings = new AppSettings { LastColor = ArgbColor.Blue };

        Assert.Equal(AppSettings.PackColor(ArgbColor.Blue), settings.LastColorArgb);
        Assert.Equal(ArgbColor.Blue, settings.LastColor);
    }

    [Theory]
    [InlineData(0, AppSettings.MinJpegQuality)]
    [InlineData(250, AppSettings.MaxJpegQuality)]
    [InlineData(75, 75)]
    public void Normalize_ClampsJpegQuality(int value, int expected)
    {
        var settings = new AppSettings { JpegQuality = value };

        settings.Normalize();

        Assert.Equal(expected, settings.JpegQuality);
    }

    [Theory]
    [InlineData(0.0, AppSettings.MinThickness)]
    [InlineData(999.0, AppSettings.MaxThickness)]
    [InlineData(7.0, 7.0)]
    public void Normalize_ClampsThickness(double value, double expected)
    {
        var settings = new AppSettings { LastThickness = value };

        settings.Normalize();

        Assert.Equal(expected, settings.LastThickness);
    }

    [Theory]
    [InlineData(1.0, AppSettings.MinTextSize)]
    [InlineData(500.0, AppSettings.MaxTextSize)]
    [InlineData(24.0, 24.0)]
    public void Normalize_ClampsTextSize(double value, double expected)
    {
        var settings = new AppSettings { LastTextSize = value };

        settings.Normalize();

        Assert.Equal(expected, settings.LastTextSize);
    }

    [Fact]
    public void Normalize_ResetsUnknownEnumsAndEmptyHotkey()
    {
        var settings = new AppSettings
        {
            Language = (LanguageOption)99,
            DefaultFormat = (ImageExportFormat)42,
            Hotkey = "   ",
        };

        settings.Normalize();

        Assert.Equal(LanguageOption.System, settings.Language);
        Assert.Equal(ImageExportFormat.Png, settings.DefaultFormat);
        Assert.Equal(AppSettings.DefaultHotkey, settings.Hotkey);
    }
}
