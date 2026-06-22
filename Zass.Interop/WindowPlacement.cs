using Zass.Interop.Native;

namespace Zass.Interop;

/// <summary>
/// Positions a window using physical-pixel coordinates, bypassing WPF's DIP-based
/// layout so the overlay covers the target monitor exactly (DPI v2 correctness).
/// </summary>
public static class WindowPlacement
{
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

    /// <summary>Moves and sizes the window to the given physical-pixel rectangle, topmost.</summary>
    public static void CoverPhysicalRect(IntPtr hWnd, PhysicalRect bounds)
    {
        NativeMethods.SetWindowPos(hWnd, HWND_TOPMOST,
            bounds.X, bounds.Y, bounds.Width, bounds.Height,
            SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }
}
