using System.Diagnostics;
using Microsoft.Win32;

namespace Zass.App.Startup;

/// <summary>
/// Manages the optional "start with Windows" entry (RF-19) as a value under
/// <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>. HKCU needs no admin
/// rights (Architecture §10). The current executable path is (re)written when
/// enabling, so the entry stays valid if the app is moved.
/// </summary>
internal static class WindowsStartup
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Zass";

    /// <summary>True if a Run entry for Zass currently exists.</summary>
    public static bool IsEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string;
    }

    /// <summary>
    /// Creates or removes the Run entry to match <paramref name="enabled"/>. Failures
    /// (e.g. a locked registry) are logged rather than thrown so a settings save is
    /// never derailed by the startup toggle.
    /// </summary>
    public static void Set(bool enabled)
    {
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (enabled)
            {
                key.SetValue(ValueName, $"\"{ExecutablePath}\"");
            }
            else if (key.GetValue(ValueName) is not null)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Zass: could not update the start-with-Windows entry: {ex.Message}");
        }
    }

    private static string ExecutablePath =>
        Environment.ProcessPath
        ?? Process.GetCurrentProcess().MainModule?.FileName
        ?? "Zass.exe";
}
