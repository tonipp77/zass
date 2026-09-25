using Zass.Core.Annotations;
using Zass.Core.Export;

namespace Zass.App.Overlay;

/// <summary>
/// Capture style carried over from the previous overlay, plus persisted save
/// preferences. Color, stroke thickness, and text size follow RF-21.
/// </summary>
public sealed record OverlayOptions(
    ArgbColor InitialColor,
    double InitialThickness,
    double InitialTextSize,
    double InitialArrowThickness,
    double InitialPixelBlockSize,
    bool InitialShadow,
    ArrowStyle InitialArrowStyle,
    ImageExportFormat DefaultFormat,
    int JpegQuality);
