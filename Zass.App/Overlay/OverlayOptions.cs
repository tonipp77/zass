using Zass.Core.Annotations;
using Zass.Core.Export;

namespace Zass.App.Overlay;

/// <summary>
/// Per-capture defaults handed to the overlay from persisted settings (RF-21): the
/// initial annotation style (carried over from the previous session), the format
/// pre-selected in the Save dialog, and the JPEG quality used when saving as JPG.
/// </summary>
public sealed record OverlayOptions(
    ArgbColor InitialColor,
    double InitialThickness,
    double InitialTextSize,
    ImageExportFormat DefaultFormat,
    int JpegQuality);
