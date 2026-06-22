using Zass.Interop;

namespace Zass.Core.Capture;

/// <summary>
/// Pure geometry helpers for turning a drag (two physical-pixel points) into a
/// normalized, clamped selection rectangle. No UI dependency: unit-testable.
/// </summary>
public static class SelectionGeometry
{
    /// <summary>
    /// Minimum side length (physical px) below which a selection is considered
    /// an accidental click and ignored (PRD §9).
    /// </summary>
    public const int MinSize = 5;

    /// <summary>
    /// Builds a rectangle from two corner points regardless of drag direction.
    /// </summary>
    public static PhysicalRect Normalize(int x1, int y1, int x2, int y2)
    {
        int x = Math.Min(x1, x2);
        int y = Math.Min(y1, y2);
        int w = Math.Abs(x2 - x1);
        int h = Math.Abs(y2 - y1);
        return new PhysicalRect(x, y, w, h);
    }

    /// <summary>
    /// Clamps a selection so it stays fully inside the monitor bounds. The
    /// selection is expressed in the same coordinate space as <paramref name="bounds"/>.
    /// </summary>
    public static PhysicalRect ClampToBounds(PhysicalRect selection, PhysicalRect bounds)
    {
        int left = Math.Max(selection.X, bounds.X);
        int top = Math.Max(selection.Y, bounds.Y);
        int right = Math.Min(selection.Right, bounds.Right);
        int bottom = Math.Min(selection.Bottom, bounds.Bottom);

        int w = Math.Max(0, right - left);
        int h = Math.Max(0, bottom - top);
        return new PhysicalRect(left, top, w, h);
    }

    /// <summary>True when the selection is large enough to be considered intentional.</summary>
    public static bool IsValidSize(PhysicalRect selection)
        => selection.Width >= MinSize && selection.Height >= MinSize;
}
