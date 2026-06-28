using System.IO;
using Zass.Core.Annotations;
using Zass.Core.Export;
using Zass.Core.Settings;

namespace Zass.Tests;

public class SettingsStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public SettingsStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ZassTests_" + Guid.NewGuid().ToString("N"));
        _path = Path.Combine(_dir, "settings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var store = new SettingsStore(_path);

        AppSettings settings = store.Load();

        Assert.Equal(LanguageOption.System, settings.Language);
        Assert.Equal(ImageExportFormat.Png, settings.DefaultFormat);
        Assert.False(File.Exists(_path)); // Load must not create the file.
    }

    [Fact]
    public void Save_CreatesFileAndParentFolder()
    {
        var store = new SettingsStore(_path);

        store.Save(new AppSettings());

        Assert.True(File.Exists(_path));
    }

    [Fact]
    public void SaveThenLoad_RoundTripsEveryPreference()
    {
        var store = new SettingsStore(_path);
        var original = new AppSettings
        {
            Language = LanguageOption.Spanish,
            DefaultFormat = ImageExportFormat.Jpeg,
            JpegQuality = 72,
            StartWithWindows = true,
            LastColor = ArgbColor.Green,
            LastThickness = 9,
            LastTextSize = 30,
        };

        store.Save(original);
        AppSettings loaded = store.Load();

        Assert.Equal(LanguageOption.Spanish, loaded.Language);
        Assert.Equal(ImageExportFormat.Jpeg, loaded.DefaultFormat);
        Assert.Equal(72, loaded.JpegQuality);
        Assert.True(loaded.StartWithWindows);
        Assert.Equal(ArgbColor.Green, loaded.LastColor);
        Assert.Equal(9, loaded.LastThickness);
        Assert.Equal(30, loaded.LastTextSize);
    }

    [Fact]
    public void Save_WritesReadableEnumNames()
    {
        var store = new SettingsStore(_path);

        store.Save(new AppSettings { Language = LanguageOption.Spanish, DefaultFormat = ImageExportFormat.Jpeg });

        string json = File.ReadAllText(_path);
        Assert.Contains("\"Spanish\"", json);
        Assert.Contains("\"Jpeg\"", json);
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaultsWithoutThrowing()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_path, "{ this is not valid json ");
        var store = new SettingsStore(_path);

        AppSettings settings = store.Load();

        Assert.Equal(LanguageOption.System, settings.Language);
        Assert.Equal(AppSettings.DefaultJpegQuality, settings.JpegQuality);
    }

    [Fact]
    public void Load_OutOfRangeValues_AreNormalized()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_path, "{ \"JpegQuality\": 5000, \"LastThickness\": 999, \"LastTextSize\": 1 }");
        var store = new SettingsStore(_path);

        AppSettings settings = store.Load();

        Assert.Equal(AppSettings.MaxJpegQuality, settings.JpegQuality);
        Assert.Equal(AppSettings.MaxThickness, settings.LastThickness);
        Assert.Equal(AppSettings.MinTextSize, settings.LastTextSize);
    }
}
