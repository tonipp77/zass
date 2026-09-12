using System.Runtime.InteropServices;
using Zass.Interop.Native;

namespace Zass.Interop;

/// <summary>Coordinates our windows with the desktop compositor before capture.</summary>
public static class WindowComposition
{
    public static void DisableTransitions(IntPtr window)
    {
        const uint transitionsForceDisabled = 3;
        int disabled = 1;
        Marshal.ThrowExceptionForHR(NativeMethods.DwmSetWindowAttribute(window, transitionsForceDisabled, in disabled, sizeof(int)));
    }

    /// <summary>Waits for the compositor to present pending updates from this application.</summary>
    public static void Flush() => Marshal.ThrowExceptionForHR(NativeMethods.DwmFlush());
}
