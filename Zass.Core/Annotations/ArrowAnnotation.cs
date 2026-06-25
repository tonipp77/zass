using Zass.Interop;

namespace Zass.Core.Annotations;

/// <summary>
/// A straight arrow from <see cref="From"/> to <see cref="To"/>. The arrowhead is
/// a presentation concern; the model keeps just the segment, color and thickness.
/// Hit-testing measures distance to the segment.
/// </summary>
public sealed class ArrowAnnotation : Annotation
{
    public ArrowAnnotation(PhysicalPoint from, PhysicalPoint to)
    {
        From = from;
        To = to;
    }

    /// <summary>Tail of the arrow.</summary>
    public PhysicalPoint From { get; set; }

    /// <summary>Head of the arrow.</summary>
    public PhysicalPoint To { get; set; }

    public override PhysicalRect Bounds => Geometry.EnclosingRect(From, To, Thickness / 2.0);

    public override void Move(double dx, double dy)
    {
        From = From.Offset(dx, dy);
        To = To.Offset(dx, dy);
    }

    public override bool HitTest(PhysicalPoint point, double tolerance = DefaultHitTolerance)
        => Geometry.DistanceToSegment(point, From, To) <= HitBand(tolerance);
}
