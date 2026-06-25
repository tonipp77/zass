using Zass.Interop;

namespace Zass.Core.Annotations;

/// <summary>
/// A hollow (outline) rectangle defined by two opposite corners. Drag direction
/// is irrelevant; bounds are normalized. Hit-testing matches the perimeter band,
/// not the hollow interior.
/// </summary>
public sealed class RectangleAnnotation : Annotation
{
    public RectangleAnnotation(PhysicalPoint start, PhysicalPoint end)
    {
        Start = start;
        End = end;
    }

    /// <summary>One corner of the rectangle (the drag origin).</summary>
    public PhysicalPoint Start { get; set; }

    /// <summary>The opposite corner (the drag end).</summary>
    public PhysicalPoint End { get; set; }

    private double Left => Math.Min(Start.X, End.X);
    private double Top => Math.Min(Start.Y, End.Y);
    private double Right => Math.Max(Start.X, End.X);
    private double Bottom => Math.Max(Start.Y, End.Y);

    public override PhysicalRect Bounds => Geometry.EnclosingRect(Start, End, Thickness / 2.0);

    public override void Move(double dx, double dy)
    {
        Start = Start.Offset(dx, dy);
        End = End.Offset(dx, dy);
    }

    public override bool HitTest(PhysicalPoint point, double tolerance = DefaultHitTolerance)
    {
        double band = HitBand(tolerance);
        bool insideOuter = point.X >= Left - band && point.X <= Right + band
                        && point.Y >= Top - band && point.Y <= Bottom + band;
        bool insideHollow = point.X > Left + band && point.X < Right - band
                         && point.Y > Top + band && point.Y < Bottom - band;
        return insideOuter && !insideHollow;
    }
}
