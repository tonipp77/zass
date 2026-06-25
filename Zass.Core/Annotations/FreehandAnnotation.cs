using Zass.Interop;

namespace Zass.Core.Annotations;

/// <summary>
/// A single freehand stroke: an ordered polyline of points. Per Arquitectura §6.2
/// and §7.2 the whole stroke is one object and one undo step — there is no
/// per-point editing in v1. Hit-testing measures distance to the nearest segment.
/// </summary>
public sealed class FreehandAnnotation : Annotation
{
    private readonly List<PhysicalPoint> _points = new();

    public FreehandAnnotation()
    {
    }

    public FreehandAnnotation(IEnumerable<PhysicalPoint> points)
    {
        _points.AddRange(points);
    }

    /// <summary>The stroke's points, in drawing order.</summary>
    public IReadOnlyList<PhysicalPoint> Points => _points;

    /// <summary>Appends a point to the stroke (used while the stroke is being drawn).</summary>
    public void AddPoint(PhysicalPoint point) => _points.Add(point);

    public override PhysicalRect Bounds => Geometry.EnclosingRect(_points, Thickness / 2.0);

    public override void Move(double dx, double dy)
    {
        for (int i = 0; i < _points.Count; i++)
        {
            _points[i] = _points[i].Offset(dx, dy);
        }
    }

    public override bool HitTest(PhysicalPoint point, double tolerance = DefaultHitTolerance)
    {
        if (_points.Count == 0)
        {
            return false;
        }

        double band = HitBand(tolerance);
        if (_points.Count == 1)
        {
            return point.DistanceTo(_points[0]) <= band;
        }

        for (int i = 0; i < _points.Count - 1; i++)
        {
            if (Geometry.DistanceToSegment(point, _points[i], _points[i + 1]) <= band)
            {
                return true;
            }
        }

        return false;
    }
}
