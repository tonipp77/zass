namespace Zass.Interop;

/// <summary>
/// Raw pixel data of a captured region, in 32-bit BGR (top-down rows).
/// The alpha byte is undefined (BitBlt does not set it), so consumers must
/// treat this as <c>Bgr32</c>, not <c>Bgra32</c>.
/// </summary>
public sealed class CapturedBitmapData
{
    public CapturedBitmapData(int width, int height, byte[] pixels)
    {
        Width = width;
        Height = height;
        Pixels = pixels;
        Stride = width * 4;
    }

    /// <summary>Width in physical pixels.</summary>
    public int Width { get; }

    /// <summary>Height in physical pixels.</summary>
    public int Height { get; }

    /// <summary>Row stride in bytes (Width * 4).</summary>
    public int Stride { get; }

    /// <summary>Top-down 32bpp BGR pixel buffer, length = Stride * Height.</summary>
    public byte[] Pixels { get; }
}
