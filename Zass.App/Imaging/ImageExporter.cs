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
    /// <summary>
    /// Writes <paramref name="image"/> to <paramref name="path"/> in the chosen format.
    /// <paramref name="jpegQuality"/> (1–100) applies only to JPEG output (RF-16, Settings).
    /// </summary>
    public static void Save(BitmapSource image, string path, ImageExportFormat format, int jpegQuality)
    {
        BitmapEncoder encoder = format switch
        {
            ImageExportFormat.Jpeg => new JpegBitmapEncoder { QualityLevel = jpegQuality },
            _ => new PngBitmapEncoder(),
        };

        encoder.Frames.Add(BitmapFrame.Create(image));
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
    }
}
