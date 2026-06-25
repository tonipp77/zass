namespace Zass.Core.Capture;

/// <summary>
/// Which part of the selection a pointer is interacting with: one of the eight
/// resize handles, the interior (for moving), or nothing.
/// </summary>
public enum SelectionHandle
{
    None,
    Inside,
    TopLeft,
    Top,
    TopRight,
    Right,
    BottomRight,
    Bottom,
    BottomLeft,
    Left,
}
