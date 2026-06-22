namespace Zass.Core.Capture;

/// <summary>Captures the monitor currently under the cursor.</summary>
public interface IScreenCaptureService
{
    /// <summary>
    /// Resolves the active monitor and returns a frozen capture of it, in
    /// physical pixels.
    /// </summary>
    CapturedImage CaptureActiveMonitor();
}
