namespace Zass.Interop;

/// <summary>
/// A rectangle expressed in <b>physical pixels</b> in virtual-desktop coordinates.
/// This is the unit the capture/overlay model works in; conversion to WPF DIPs
/// happens only at the presentation layer.
/// </summary>
public readonly record struct PhysicalRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public bool IsEmpty => Width <= 0 || Height <= 0;
}
