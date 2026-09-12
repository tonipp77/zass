using Zass.Interop;

namespace Zass.Core.Annotations;

/// <summary>Editable pixelation rectangle. Its raster effect is owned by presentation.</summary>
public sealed class PixelationAnnotation(PhysicalPoint start, PhysicalPoint end) : Annotation
{
    public PhysicalPoint Start { get; set; } = start;
    public PhysicalPoint End { get; set; } = end;
    public int BlockSize { get; init; } = 24;
    public override PhysicalRect Bounds => Geometry.EnclosingRect(Start, End, 0);
    public override void Move(double dx, double dy)
    {
        Start = Start.Offset(dx, dy);
        End = End.Offset(dx, dy);
    }
    public override bool HitTest(PhysicalPoint p, double tolerance = DefaultHitTolerance)
        => p.X >= Bounds.X - tolerance && p.X <= Bounds.Right + tolerance &&
           p.Y >= Bounds.Y - tolerance && p.Y <= Bounds.Bottom + tolerance;
}
