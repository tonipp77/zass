namespace Zass.App.Overlay;

/// <summary>
/// The active overlay tool. <see cref="Pointer"/> edits the selection; the
/// remaining values draw vector annotations. <see cref="Rectangle"/> arrived in
/// Increment 3; <see cref="Arrow"/>, <see cref="FilledRectangle"/>,
/// <see cref="Freehand"/> and <see cref="Text"/> in Increment 4.
/// </summary>
internal enum OverlayTool
{
    Pointer,
    Text,
    Arrow,
    Line,
    Rectangle,
    FilledRectangle,
    Freehand,
    Pixelation,
}
