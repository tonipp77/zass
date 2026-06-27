using System.Globalization;
using System.IO;

namespace Zass.Core.Export;

/// <summary>
/// Pure, UI-free export helpers: the default time-stamped file name (RF-17) and the
/// mapping from a file extension to an output format (RF-16). The actual WPF encoding
/// lives in the presentation layer (Zass.App) so that Zass.Core stays free of WPF.
/// </summary>
public static class ExportNaming
{
    /// <summary>
    /// Default save name with a sortable timestamp, e.g. <c>Zass_2026-06-27_142233.png</c>.
    /// The timestamp is formatted with the invariant culture so it never localizes.
    /// </summary>
    public static string DefaultFileName(DateTime timestamp) =>
        "Zass_" + timestamp.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture) + ".png";

    /// <summary>Resolves the output format from a path's extension; PNG is the fallback.</summary>
    public static ImageExportFormat FormatFromExtension(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => ImageExportFormat.Jpeg,
            _ => ImageExportFormat.Png,
        };
}
