namespace Zass.Core.Annotations;

/// <summary>
/// A point in <b>physical pixels</b> (virtual-desktop coordinates), matching the
/// project rule that the model works in physical pixels and conversion to WPF
/// DIPs happens only at the presentation layer. Uses <see cref="double"/> so that
/// sub-pixel precision survives while a stroke is being drawn.
/// </summary>
public readonly record struct PhysicalPoint(double X, double Y)
{
    /// <summary>Returns a copy translated by the given delta.</summary>
    public PhysicalPoint Offset(double dx, double dy) => new(X + dx, Y + dy);

    /// <summary>Euclidean distance to another point.</summary>
    public double DistanceTo(PhysicalPoint other)
    {
        double dx = other.X - X;
        double dy = other.Y - Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
