using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Zass.Core.Annotations;

namespace Zass.App.Imaging;

internal static class PixelationRenderer
{
    public static BitmapSource Create(BitmapSource source, Int32Rect region, int blockSize)
    {
        var crop = new CroppedBitmap(source, region);
        var converted = new FormatConvertedBitmap(crop, PixelFormats.Pbgra32, null, 0);
        int stride = checked(region.Width * 4);
        var pixels = new byte[checked(stride * region.Height)];
        converted.CopyPixels(pixels, stride, 0);
        PixelationEffect.Apply(pixels, region.Width, region.Height, stride, blockSize);
        var result = BitmapSource.Create(region.Width, region.Height, 96, 96,
            PixelFormats.Pbgra32, null, pixels, stride);
        result.Freeze();
        return result;
    }
}
