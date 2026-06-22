using Zass.Interop;

namespace Zass.Core.Capture;

/// <summary>
/// Default capture service: finds the monitor under the cursor and BitBlts it
/// into a frozen bitmap. All work happens in physical pixels.
/// </summary>
public sealed class ScreenCaptureService : IScreenCaptureService
{
    public CapturedImage CaptureActiveMonitor()
    {
        MonitorDescription monitor = MonitorInfoProvider.GetMonitorAtCursor();
        CapturedBitmapData bitmap = ScreenCapture.CaptureRegion(monitor.Bounds);
        return new CapturedImage(bitmap, monitor.Bounds, monitor.DpiX, monitor.DpiY);
    }
}
