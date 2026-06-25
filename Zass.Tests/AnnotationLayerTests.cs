using Zass.Core.Annotations;

namespace Zass.Tests;

public class AnnotationLayerTests
{
    private static PhysicalPoint P(double x, double y) => new(x, y);

    [Fact]
    public void Add_AppendsOnTop()
    {
        var layer = new AnnotationLayer();
        var a = new RectangleAnnotation(P(0, 0), P(1, 1));
        var b = new RectangleAnnotation(P(0, 0), P(1, 1));

        layer.Add(a);
        layer.Add(b);

        Assert.Equal(0, layer.IndexOf(a));
        Assert.Equal(1, layer.IndexOf(b));
    }

    [Fact]
    public void Insert_ClampsOutOfRangeIndex()
    {
        var layer = new AnnotationLayer();
        var a = new RectangleAnnotation(P(0, 0), P(1, 1));

        layer.Insert(99, a);

        Assert.Equal(0, layer.IndexOf(a));
    }

    [Fact]
    public void HitTest_ReturnsTopmostMatch()
    {
        var layer = new AnnotationLayer();
        var bottom = new FilledRectangleAnnotation(P(0, 0), P(100, 100));
        var top = new FilledRectangleAnnotation(P(0, 0), P(100, 100));
        layer.Add(bottom);
        layer.Add(top);

        Assert.Same(top, layer.HitTest(P(50, 50)));
    }

    [Fact]
    public void HitTest_ReturnsNullWhenNothingHit()
    {
        var layer = new AnnotationLayer();
        layer.Add(new FilledRectangleAnnotation(P(0, 0), P(10, 10)));

        Assert.Null(layer.HitTest(P(500, 500)));
    }
}
