using Zass.Interop;

namespace Zass.Core.Capture;

/// <summary>
/// Pure geometry for editing an existing selection: hit-testing the eight resize
/// handles, resizing by dragging a handle, and moving the whole selection. Works
/// entirely in physical pixels and has no UI dependency, so it is unit-testable.
/// </summary>
public static class SelectionManipulator
{
    /// <summary>Handles in hit-test priority order: corners win over edges on overlap.</summary>
    private static readonly SelectionHandle[] Priority =
    {
        SelectionHandle.TopLeft, SelectionHandle.TopRight,
        SelectionHandle.BottomRight, SelectionHandle.BottomLeft,
        SelectionHandle.Top, SelectionHandle.Right,
        SelectionHandle.Bottom, SelectionHandle.Left,
    };

    /// <summary>Center of a handle in physical pixels.</summary>
    public static (int X, int Y) HandleCenter(PhysicalRect s, SelectionHandle handle)
    {
        int midX = s.X + s.Width / 2;
        int midY = s.Y + s.Height / 2;
        return handle switch
        {
            SelectionHandle.TopLeft => (s.X, s.Y),
            SelectionHandle.Top => (midX, s.Y),
            SelectionHandle.TopRight => (s.Right, s.Y),
            SelectionHandle.Right => (s.Right, midY),
            SelectionHandle.BottomRight => (s.Right, s.Bottom),
            SelectionHandle.Bottom => (midX, s.Bottom),
            SelectionHandle.BottomLeft => (s.X, s.Bottom),
            SelectionHandle.Left => (s.X, midY),
            _ => (midX, midY),
        };
    }

    /// <summary>
    /// Returns the handle under <paramref name="px"/>,<paramref name="py"/> within a
    /// square <paramref name="handleRadius"/>; otherwise <see cref="SelectionHandle.Inside"/>
    /// if the point is within the selection, else <see cref="SelectionHandle.None"/>.
    /// </summary>
    public static SelectionHandle HitTest(PhysicalRect s, int px, int py, int handleRadius)
    {
        if (s.IsEmpty)
        {
            return SelectionHandle.None;
        }

        foreach (SelectionHandle handle in Priority)
        {
            (int hx, int hy) = HandleCenter(s, handle);
            if (Math.Abs(px - hx) <= handleRadius && Math.Abs(py - hy) <= handleRadius)
            {
                return handle;
            }
        }

        bool inside = px >= s.X && px <= s.Right && py >= s.Y && py <= s.Bottom;
        return inside ? SelectionHandle.Inside : SelectionHandle.None;
    }

    /// <summary>
    /// Resizes the selection by dragging <paramref name="handle"/> to the pointer.
    /// The opposite edge(s) stay fixed; the moving edge is clamped to the monitor
    /// <paramref name="bounds"/> and kept at least <paramref name="minSize"/> away from
    /// its opposite edge (no flipping past it in v1).
    /// </summary>
    public static PhysicalRect Resize(
        PhysicalRect s, SelectionHandle handle, int px, int py, PhysicalRect bounds, int minSize)
    {
        int left = s.X, top = s.Y, right = s.Right, bottom = s.Bottom;

        bool movesLeft = handle is SelectionHandle.TopLeft or SelectionHandle.Left or SelectionHandle.BottomLeft;
        bool movesRight = handle is SelectionHandle.TopRight or SelectionHandle.Right or SelectionHandle.BottomRight;
        bool movesTop = handle is SelectionHandle.TopLeft or SelectionHandle.Top or SelectionHandle.TopRight;
        bool movesBottom = handle is SelectionHandle.BottomLeft or SelectionHandle.Bottom or SelectionHandle.BottomRight;

        if (movesLeft)
        {
            left = Math.Clamp(px, bounds.X, right - minSize);
        }
        if (movesRight)
        {
            right = Math.Clamp(px, left + minSize, bounds.Right);
        }
        if (movesTop)
        {
            top = Math.Clamp(py, bounds.Y, bottom - minSize);
        }
        if (movesBottom)
        {
            bottom = Math.Clamp(py, top + minSize, bounds.Bottom);
        }

        return new PhysicalRect(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// Translates the whole selection by (<paramref name="dx"/>,<paramref name="dy"/>),
    /// clamping its position so it stays fully inside <paramref name="bounds"/> without
    /// changing size.
    /// </summary>
    public static PhysicalRect Move(PhysicalRect s, int dx, int dy, PhysicalRect bounds)
    {
        int maxX = bounds.Right - s.Width;
        int maxY = bounds.Bottom - s.Height;

        int x = maxX >= bounds.X ? Math.Clamp(s.X + dx, bounds.X, maxX) : bounds.X;
        int y = maxY >= bounds.Y ? Math.Clamp(s.Y + dy, bounds.Y, maxY) : bounds.Y;

        return new PhysicalRect(x, y, s.Width, s.Height);
    }
}
