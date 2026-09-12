using Zass.Core.Annotations;
using Zass.Core.Commands;
using Zass.Interop;

namespace Zass.Tests;

public class LineAnnotationTests
{
    [Fact]
    public void ReversedLine_HasPaddedBoundsAndSegmentHitTesting()
    {
        var line = new LineAnnotation(new(100, 80), new(20, 20)) { Thickness = 4 };
        Assert.Equal(new PhysicalRect(18, 18, 84, 64), line.Bounds);
        Assert.True(line.HitTest(new(60, 50)));
        Assert.False(line.HitTest(new(20, 80)));
    }

    [Fact]
    public void WholeLineAndStyle_SurviveUndoRedoAlongsideOtherAnnotations()
    {
        var layer = new AnnotationLayer();
        var history = new UndoRedoManager();
        var line = new LineAnnotation(new(0, 0), new(100, 0)) { Thickness = 12, Color = new(255, 0, 120, 255) };
        history.Execute(new AddAnnotationCommand(layer, new ArrowAnnotation(new(1, 1), new(5, 5))));
        history.Execute(new AddAnnotationCommand(layer, line));
        history.Undo();
        Assert.Single(layer.Items);
        history.Redo();
        Assert.Same(line, layer.Items[1]);
        Assert.Equal(12, line.Thickness);
        Assert.Equal(new ArgbColor(255, 0, 120, 255), line.Color);
        history.Execute(new MoveAnnotationCommand(line, 4, 8));
        Assert.Equal(new PhysicalPoint(104, 8), line.To);
        history.Undo();
        Assert.Equal(new PhysicalPoint(100, 0), line.To);
    }
}
