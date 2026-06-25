namespace Zass.App.Overlay;

/// <summary>
/// The active overlay tool. Increment 3 wires up <see cref="Pointer"/> (edit the
/// selection) and <see cref="Rectangle"/> (draw an outline rectangle annotation);
/// the remaining tools arrive in later increments.
/// </summary>
internal enum OverlayTool
{
    Pointer,
    Rectangle,
}
