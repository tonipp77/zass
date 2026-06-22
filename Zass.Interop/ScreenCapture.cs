using Zass.Interop.Native;

namespace Zass.Interop;

/// <summary>GDI BitBlt screen capture working entirely in physical pixels.</summary>
public static class ScreenCapture
{
    /// <summary>
    /// Captures the given region (physical pixels, virtual-desktop coordinates)
    /// into a top-down 32bpp BGR buffer.
    /// </summary>
    public static CapturedBitmapData CaptureRegion(PhysicalRect region)
    {
        if (region.IsEmpty)
        {
            throw new ArgumentException("Capture region must have a positive size.", nameof(region));
        }

        int w = region.Width;
        int h = region.Height;

        IntPtr screenDc = NativeMethods.GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            throw new InvalidOperationException("GetDC failed.");
        }

        IntPtr memDc = IntPtr.Zero;
        IntPtr bmp = IntPtr.Zero;
        IntPtr oldBmp = IntPtr.Zero;
        try
        {
            memDc = NativeMethods.CreateCompatibleDC(screenDc);
            bmp = NativeMethods.CreateCompatibleBitmap(screenDc, w, h);
            if (memDc == IntPtr.Zero || bmp == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to create compatible GDI objects.");
            }

            oldBmp = NativeMethods.SelectObject(memDc, bmp);

            if (!NativeMethods.BitBlt(memDc, 0, 0, w, h, screenDc, region.X, region.Y,
                    NativeMethods.SRCCOPY | NativeMethods.CAPTUREBLT))
            {
                throw new InvalidOperationException("BitBlt failed.");
            }

            var header = new BITMAPINFOHEADER
            {
                biSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = w,
                biHeight = -h, // negative => top-down rows
                biPlanes = 1,
                biBitCount = 32,
                biCompression = NativeMethods.BI_RGB,
            };

            var pixels = new byte[w * 4 * h];
            int scanned = NativeMethods.GetDIBits(memDc, bmp, 0, (uint)h, pixels, ref header,
                NativeMethods.DIB_RGB_COLORS);
            if (scanned == 0)
            {
                throw new InvalidOperationException("GetDIBits failed.");
            }

            return new CapturedBitmapData(w, h, pixels);
        }
        finally
        {
            if (oldBmp != IntPtr.Zero)
            {
                NativeMethods.SelectObject(memDc, oldBmp);
            }
            if (bmp != IntPtr.Zero)
            {
                NativeMethods.DeleteObject(bmp);
            }
            if (memDc != IntPtr.Zero)
            {
                NativeMethods.DeleteDC(memDc);
            }
            NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }
}
