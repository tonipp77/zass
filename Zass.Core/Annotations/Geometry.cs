using Zass.Interop;

namespace Zass.Core.Annotations;

/// <summary>
/// Pure geometry helpers shared by the annotation shapes. No UI dependency.
/// </summary>
internal static class Geometry
{
    /// <summary>
    /// Shortest distance from <paramref name="p"/> to the segment a→b. If the
    /// segment is degenerate (a == b) returns the distance to that point.
    /// </summary>
    public static double DistanceToSegment(PhysicalPoint p, PhysicalPoint a, PhysicalPoint b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        double lengthSq = dx * dx + dy * dy;
        if (lengthSq == 0)
        {
            return p.DistanceTo(a);
        }

        double t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lengthSq;
        t = Math.Clamp(t, 0.0, 1.0);
        var projection = new PhysicalPoint(a.X + t * dx, a.Y + t * dy);
        return p.DistanceTo(projection);
    }

    /// <summary>
    /// Integer enclosing rectangle (rounded outward) of two points, expanded by
    /// <paramref name="padding"/> on every side (typically half the stroke width).
    /// </summary>
    public static PhysicalRect EnclosingRect(PhysicalPoint a, PhysicalPoint b, double padding)
        => EnclosingRect(new[] { a, b }, padding);

    /// <summary>
    /// Integer enclosing rectangle (rounded outward) of a set of points, expanded
    /// by <paramref name="padding"/> on every side.
    /// </summary>
    public static PhysicalRect EnclosingRect(IReadOnlyList<PhysicalPoint> points, double padding)
    {
        if (points.Count == 0)
        {
            return new PhysicalRect(0, 0, 0, 0);
        }

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        foreach (PhysicalPoint pt in points)
        {
            if (pt.X < minX) minX = pt.X;
            if (pt.Y < minY) minY = pt.Y;
            if (pt.X > maxX) maxX = pt.X;
            if (pt.Y > maxY) maxY = pt.Y;
        }

        minX -= padding; minY -= padding;
        maxX += padding; maxY += padding;

        int x = (int)Math.Floor(minX);
        int y = (int)Math.Floor(minY);
        int w = (int)Math.Ceiling(maxX) - x;
        int h = (int)Math.Ceiling(maxY) - y;
        return new PhysicalRect(x, y, w, h);
    }
}
