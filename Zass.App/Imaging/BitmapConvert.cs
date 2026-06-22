using System.Windows.Media;
using System.Windows.Media.Imaging;
using Zass.Core.Capture;
using Zass.Interop;

namespace Zass.App.Imaging;

/// <summary>
/// Bridges raw physical-pixel buffers (from <see cref="Zass.Interop"/>) to WPF
/// <see cref="BitmapSource"/>. This is the single presentation-layer point where
/// physical pixels meet DIP/DPI.
/// </summary>
internal static class BitmapConvert
{
    // Capture is 32bpp BGR; the alpha byte from BitBlt is undefined, so use Bgr32.
    private static readonly PixelFormat Format = PixelFormats.Bgr32;

    /// <summary>
    /// Builds the frozen background image. The DPI is set to the monitor's DPI so
    /// that a window sized to the physical bounds shows the image pixel-for-pixel.
    /// </summary>
    public static BitmapSource CreateBackground(CapturedImage capture)
    {
        CapturedBitmapData b = capture.Bitmap;
        var bmp = BitmapSource.Create(
            b.Width, b.Height,
            capture.DpiX, capture.DpiY,
            Format, null,
            b.Pixels, b.Stride);
        bmp.Freeze();
        return bmp;
    }

    /// <summary>
    /// Crops the captured monitor to <paramref name="selectionPhysical"/> (physical
    /// pixels, virtual-desktop coordinates) and returns a 96-DPI bitmap whose pixel
    /// dimensions equal the selection — ready for pixel-faithful clipboard export.
    /// </summary>
    public static BitmapSource CropForExport(CapturedImage capture, PhysicalRect selectionPhysical)
    {
        CapturedBitmapData src = capture.Bitmap;

        // Translate from virtual-desktop coordinates to offsets inside the buffer.
        int offX = selectionPhysical.X - capture.PhysicalBounds.X;
        int offY = selectionPhysical.Y - capture.PhysicalBounds.Y;
        int w = selectionPhysical.Width;
        int h = selectionPhysical.Height;

        int dstStride = w * 4;
        var dst = new byte[dstStride * h];

        for (int row = 0; row < h; row++)
        {
            int srcStart = (offY + row) * src.Stride + offX * 4;
            int dstStart = row * dstStride;
            Array.Copy(src.Pixels, srcStart, dst, dstStart, dstStride);
        }

        var bmp = BitmapSource.Create(w, h, 96, 96, Format, null, dst, dstStride);
        bmp.Freeze();
        return bmp;
    }
}
