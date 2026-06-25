using Zass.Interop;

namespace Zass.Core.Annotations;

/// <summary>
/// Base type for every editable vector annotation that lives on the overlay
/// during a capture session.
/// <para>
/// This model is deliberately <b>UI-free</b>: it carries only the logical state
/// and geometry (bounds, hit-testing, movement). Turning an annotation into a WPF
/// <c>UIElement</c> is the presentation layer's job (see Arquitectura §6.1), so
/// that <see cref="Zass.Core"/> stays free of any WPF dependency per CLAUDE.md.
/// </para>
/// </summary>
public abstract class Annotation
{
    /// <summary>Default click slack (physical px) added on top of the stroke band.</summary>
    public const double DefaultHitTolerance = 4.0;

    /// <summary>Stable identity for the lifetime of the annotation.</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Stroke color (outline/arrow/freehand/text) or fill color (filled rectangle).</summary>
    public ArgbColor Color { get; set; } = ArgbColor.Red;

    /// <summary>Stroke width in physical pixels. Ignored by shapes that have no stroke.</summary>
    public double Thickness { get; set; } = 2.0;

    /// <summary>Paint order; higher draws on top. Managed by the presentation layer.</summary>
    public int ZIndex { get; set; }

    /// <summary>Axis-aligned bounding box in physical pixels, including the stroke.</summary>
    public abstract PhysicalRect Bounds { get; }

    /// <summary>True when <paramref name="point"/> lands on the annotation, within tolerance.</summary>
    public abstract bool HitTest(PhysicalPoint point, double tolerance = DefaultHitTolerance);

    /// <summary>Translates the annotation's geometry by the given delta (physical px).</summary>
    public abstract void Move(double dx, double dy);

    /// <summary>Half the stroke width plus the caller's tolerance: the clickable band.</summary>
    protected double HitBand(double tolerance) => tolerance + Thickness / 2.0;
}
