using Zass.Interop;

namespace Zass.Core.Annotations;

/// <summary>
/// A solid rectangle defined by two opposite corners. <see cref="Annotation.Color"/>
/// is the fill color; there is no stroke, so <see cref="Annotation.Thickness"/> is
/// unused. The whole interior is clickable.
/// </summary>
public sealed class FilledRectangleAnnotation : Annotation
{
    public FilledRectangleAnnotation(PhysicalPoint start, PhysicalPoint end)
    {
        Start = start;
        End = end;
        Thickness = 0;
    }

    public PhysicalPoint Start { get; set; }
    public PhysicalPoint End { get; set; }

    private double Left => Math.Min(Start.X, End.X);
    private double Top => Math.Min(Start.Y, End.Y);
    private double Right => Math.Max(Start.X, End.X);
    private double Bottom => Math.Max(Start.Y, End.Y);

    public override PhysicalRect Bounds => Geometry.EnclosingRect(Start, End, 0);

    public override void Move(double dx, double dy)
    {
        Start = Start.Offset(dx, dy);
        End = End.Offset(dx, dy);
    }

    public override bool HitTest(PhysicalPoint point, double tolerance = DefaultHitTolerance)
        => point.X >= Left - tolerance && point.X <= Right + tolerance
        && point.Y >= Top - tolerance && point.Y <= Bottom + tolerance;
}
