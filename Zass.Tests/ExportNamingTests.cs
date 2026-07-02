using Zass.Core.Export;

namespace Zass.Tests;

public class ExportNamingTests
{
    [Fact]
    public void DefaultFileName_UsesSortableTimestampAndPngExtension()
    {
        var timestamp = new DateTime(2026, 6, 27, 14, 22, 33);
        Assert.Equal("Zass_2026-06-27_142233.png", ExportNaming.DefaultFileName(timestamp));
    }

    [Fact]
    public void DefaultFileName_PadsSingleDigitComponents()
    {
        var timestamp = new DateTime(2026, 1, 5, 9, 7, 3);
        Assert.Equal("Zass_2026-01-05_090703.png", ExportNaming.DefaultFileName(timestamp));
    }

    [Fact]
    public void DefaultFileName_WithFormat_UsesMatchingExtension()
    {
        var timestamp = new DateTime(2026, 6, 27, 14, 22, 33);

        Assert.Equal("Zass_2026-06-27_142233.png",
            ExportNaming.DefaultFileName(timestamp, ImageExportFormat.Png));
        Assert.Equal("Zass_2026-06-27_142233.jpg",
            ExportNaming.DefaultFileName(timestamp, ImageExportFormat.Jpeg));
    }

    [Theory]
    [InlineData(ImageExportFormat.Png, ".png")]
    [InlineData(ImageExportFormat.Jpeg, ".jpg")]
    public void ExtensionFor_MapsFormatToExtension(ImageExportFormat format, string expected)
    {
        Assert.Equal(expected, ExportNaming.ExtensionFor(format));
    }

    [Theory]
    [InlineData("C:\\shots\\capture.png", ImageExportFormat.Png)]
    [InlineData("C:\\shots\\capture.PNG", ImageExportFormat.Png)]
    [InlineData("C:\\shots\\capture.jpg", ImageExportFormat.Jpeg)]
    [InlineData("C:\\shots\\capture.JPG", ImageExportFormat.Jpeg)]
    [InlineData("C:\\shots\\capture.jpeg", ImageExportFormat.Jpeg)]
    [InlineData("C:\\shots\\capture", ImageExportFormat.Png)]
    [InlineData("C:\\shots\\capture.bmp", ImageExportFormat.Png)]
    public void FormatFromExtension_MapsKnownExtensions(string path, ImageExportFormat expected)
    {
        Assert.Equal(expected, ExportNaming.FormatFromExtension(path));
    }
}
