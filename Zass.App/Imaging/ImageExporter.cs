using System.IO;
using System.Windows.Media.Imaging;
using Zass.Core.Export;

namespace Zass.App.Imaging;

/// <summary>
/// Encodes a composed <see cref="BitmapSource"/> to disk as PNG or JPEG (RF-15, RF-16).
/// This is the WPF-dependent counterpart to <see cref="Zass.Core.Export.ExportNaming"/>;
/// keeping the encoders here lets <see cref="Zass.Core"/> stay free of WPF.
/// </summary>
internal static class ImageExporter
{
    /// <summary>Default JPEG quality; a configurable value arrives with Settings (Increment 7).</summary>
    private const int JpegQuality = 90;

    /// <summary>Writes <paramref name="image"/> to <paramref name="path"/> in the chosen format.</summary>
    public static void Save(BitmapSource image, string path, ImageExportFormat format)
    {
        BitmapEncoder encoder = format switch
        {
            ImageExportFormat.Jpeg => new JpegBitmapEncoder { QualityLevel = JpegQuality },
            _ => new PngBitmapEncoder(),
        };

        encoder.Frames.Add(BitmapFrame.Create(image));
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
    }
}
