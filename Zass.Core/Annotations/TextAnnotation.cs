using Zass.Interop;

namespace Zass.Core.Annotations;

/// <summary>
/// A text label anchored at <see cref="Position"/> (its top-left corner).
/// <para>
/// Text measurement requires a font/typeface, which is a WPF concern, so the
/// rendered <see cref="Width"/> and <see cref="Height"/> are written back by the
/// presentation layer after it lays the text out. The model uses them only for
/// bounds and hit-testing. <see cref="Annotation.Color"/> is the text color and
/// <see cref="Annotation.Thickness"/> is unused.
/// </para>
/// </summary>
public sealed class TextAnnotation : Annotation
{
    public TextAnnotation(PhysicalPoint position, string text, double fontSize)
    {
        Position = position;
        Text = text;
        FontSize = fontSize;
        Thickness = 0;
    }

    /// <summary>Top-left corner of the text box.</summary>
    public PhysicalPoint Position { get; set; }

    /// <summary>The text content.</summary>
    public string Text { get; set; }

    /// <summary>Font size in physical pixels.</summary>
    public double FontSize { get; set; }

    /// <summary>Rendered width (physical px), assigned by the presentation layer.</summary>
    public double Width { get; set; }

    /// <summary>Rendered height (physical px), assigned by the presentation layer.</summary>
    public double Height { get; set; }

    public override PhysicalRect Bounds => Geometry.EnclosingRect(
        Position,
        Position.Offset(Width, Height),
        0);

    public override void Move(double dx, double dy) => Position = Position.Offset(dx, dy);

    public override bool HitTest(PhysicalPoint point, double tolerance = DefaultHitTolerance)
        => point.X >= Position.X - tolerance && point.X <= Position.X + Width + tolerance
        && point.Y >= Position.Y - tolerance && point.Y <= Position.Y + Height + tolerance;
}
