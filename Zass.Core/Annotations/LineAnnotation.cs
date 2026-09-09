using Zass.Interop;

namespace Zass.Core.Annotations;

/// <summary>
/// A straight line from <see cref="From"/> to <see cref="To"/> without an arrowhead.
/// The model keeps the segment, color and thickness in physical pixels.
/// Hit-testing measures distance to the segment.
/// </summary>
public sealed class LineAnnotation : Annotation
{
    public LineAnnotation(PhysicalPoint from, PhysicalPoint to)
    {
        From = from;
        To = to;
    }

    /// <summary>Start of the line.</summary>
    public PhysicalPoint From { get; set; }

    /// <summary>End of the line.</summary>
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
