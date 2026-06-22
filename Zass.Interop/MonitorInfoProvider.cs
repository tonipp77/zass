using Zass.Interop.Native;

namespace Zass.Interop;

/// <summary>Describes the monitor that currently contains the cursor.</summary>
public readonly record struct MonitorDescription(PhysicalRect Bounds, uint DpiX, uint DpiY)
{
    /// <summary>Scale factor on the X axis (1.0 = 100%, 1.5 = 150%).</summary>
    public double ScaleX => DpiX / 96.0;

    /// <summary>Scale factor on the Y axis.</summary>
    public double ScaleY => DpiY / 96.0;
}

/// <summary>Resolves the active monitor (the one under the cursor) in physical pixels.</summary>
public static class MonitorInfoProvider
{
    /// <summary>
    /// Returns the bounds (physical pixels) and effective DPI of the monitor that
    /// contains the cursor at the moment of the call.
    /// </summary>
    public static MonitorDescription GetMonitorAtCursor()
    {
        if (!NativeMethods.GetCursorPos(out POINT cursor))
        {
            throw new InvalidOperationException("GetCursorPos failed.");
        }

        IntPtr hMonitor = NativeMethods.MonitorFromPoint(cursor, NativeMethods.MONITOR_DEFAULTTONEAREST);

        var info = new MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFOEX>() };
        if (!NativeMethods.GetMonitorInfo(hMonitor, ref info))
        {
            throw new InvalidOperationException("GetMonitorInfo failed.");
        }

        RECT r = info.rcMonitor;
        var bounds = new PhysicalRect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);

        uint dpiX = 96, dpiY = 96;
        // GetDpiForMonitor returns S_OK (0) on success; fall back to 96 otherwise.
        if (NativeMethods.GetDpiForMonitor(hMonitor, NativeMethods.MDT_EFFECTIVE_DPI, out uint dx, out uint dy) == 0)
        {
            dpiX = dx;
            dpiY = dy;
        }

        return new MonitorDescription(bounds, dpiX, dpiY);
    }
}
