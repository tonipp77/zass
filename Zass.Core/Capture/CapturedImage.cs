using Zass.Interop;

namespace Zass.Core.Capture;

/// <summary>
/// A frozen capture of the active monitor: the raw pixels plus the physical
/// bounds and DPI needed to place the overlay 1:1 over that monitor.
/// </summary>
public sealed class CapturedImage
{
    public CapturedImage(CapturedBitmapData bitmap, PhysicalRect physicalBounds, uint dpiX, uint dpiY)
    {
        Bitmap = bitmap;
        PhysicalBounds = physicalBounds;
        DpiX = dpiX;
        DpiY = dpiY;
    }

    /// <summary>Raw 32bpp BGR pixels of the whole monitor.</summary>
    public CapturedBitmapData Bitmap { get; }

    /// <summary>Monitor bounds in physical pixels (virtual-desktop coordinates).</summary>
    public PhysicalRect PhysicalBounds { get; }

    public uint DpiX { get; }
    public uint DpiY { get; }

    public double ScaleX => DpiX / 96.0;
    public double ScaleY => DpiY / 96.0;
}
