using Zass.Core.Annotations;
using Zass.Interop;

namespace Zass.Tests;

public class AnnotationTests
{
    private static PhysicalPoint P(double x, double y) => new(x, y);

    [Fact]
    public void Annotation_HasUniqueId()
    {
        var a = new RectangleAnnotation(P(0, 0), P(10, 10));
        var b = new RectangleAnnotation(P(0, 0), P(10, 10));
        Assert.NotEqual(a.Id, b.Id);
    }

    // ---- Rectangle (outline) ----

    [Fact]
    public void Rectangle_BoundsAreNormalizedAndIncludeStroke()
    {
        var rect = new RectangleAnnotation(P(100, 80), P(20, 30)) { Thickness = 4 };
        // Normalized box is (20,30)-(100,80); padded by half the 4px stroke -> 2px each side.
        Assert.Equal(new PhysicalRect(18, 28, 84, 54), rect.Bounds);
    }

    [Fact]
    public void Rectangle_HitsPerimeterButNotHollowInterior()
    {
        var rect = new RectangleAnnotation(P(0, 0), P(100, 100)) { Thickness = 2 };
        Assert.True(rect.HitTest(P(0, 50)));     // on the left edge
        Assert.True(rect.HitTest(P(100, 100)));  // on a corner
        Assert.False(rect.HitTest(P(50, 50)));   // in the hollow center
    }

    [Fact]
    public void Rectangle_MoveTranslatesBothCorners()
    {
        var rect = new RectangleAnnotation(P(0, 0), P(10, 10));
        rect.Move(5, -3);
        Assert.Equal(P(5, -3), rect.Start);
        Assert.Equal(P(15, 7), rect.End);
    }

    // ---- Filled rectangle ----

    [Fact]
    public void FilledRectangle_HitsInterior()
    {
        var rect = new FilledRectangleAnnotation(P(0, 0), P(100, 100));
        Assert.True(rect.HitTest(P(50, 50)));
        Assert.False(rect.HitTest(P(200, 200)));
    }

    [Fact]
    public void FilledRectangle_BoundsHaveNoStrokePadding()
    {
        var rect = new FilledRectangleAnnotation(P(10, 10), P(40, 30));
        Assert.Equal(new PhysicalRect(10, 10, 30, 20), rect.Bounds);
    }

    // ---- Arrow ----

    [Fact]
    public void Arrow_HitsAlongTheSegment()
    {
        var arrow = new ArrowAnnotation(P(0, 0), P(100, 0)) { Thickness = 2 };
        Assert.True(arrow.HitTest(P(50, 1)));    // 1px off the line, within band
        Assert.False(arrow.HitTest(P(50, 40)));  // far from the line
    }

    [Fact]
    public void Arrow_MoveTranslatesEndpoints()
    {
        var arrow = new ArrowAnnotation(P(0, 0), P(10, 10));
        arrow.Move(2, 2);
        Assert.Equal(P(2, 2), arrow.From);
        Assert.Equal(P(12, 12), arrow.To);
    }

    // ---- Freehand ----

    [Fact]
    public void Freehand_HitsNearAnySegment()
    {
        var stroke = new FreehandAnnotation(new[] { P(0, 0), P(10, 0), P(10, 10) }) { Thickness = 2 };
        Assert.True(stroke.HitTest(P(5, 0)));    // on first segment
        Assert.True(stroke.HitTest(P(10, 5)));   // on second segment
        Assert.False(stroke.HitTest(P(50, 50))); // nowhere near
    }

    [Fact]
    public void Freehand_BoundsEncloseAllPoints()
    {
        var stroke = new FreehandAnnotation(new[] { P(5, 5), P(20, 50), P(0, 30) });
        stroke.Thickness = 0;
        Assert.Equal(new PhysicalRect(0, 5, 20, 45), stroke.Bounds);
    }

    [Fact]
    public void Freehand_MoveTranslatesEveryPoint()
    {
        var stroke = new FreehandAnnotation(new[] { P(0, 0), P(10, 10) });
        stroke.Move(1, 2);
        Assert.Equal(P(1, 2), stroke.Points[0]);
        Assert.Equal(P(11, 12), stroke.Points[1]);
    }

    [Fact]
    public void Freehand_EmptyStrokeDoesNotHit()
    {
        var stroke = new FreehandAnnotation();
        Assert.False(stroke.HitTest(P(0, 0)));
    }

    // ---- Text ----

    [Fact]
    public void Text_HitsInsideMeasuredBox()
    {
        var text = new TextAnnotation(P(10, 10), "hello", 16) { Width = 80, Height = 20 };
        Assert.True(text.HitTest(P(50, 20)));
        Assert.False(text.HitTest(P(200, 200)));
    }

    [Fact]
    public void Text_MoveTranslatesPosition()
    {
        var text = new TextAnnotation(P(10, 10), "hi", 16) { Width = 40, Height = 20 };
        text.Move(5, 5);
        Assert.Equal(P(15, 15), text.Position);
        Assert.Equal(new PhysicalRect(15, 15, 40, 20), text.Bounds);
    }
}
