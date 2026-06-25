using Zass.Core.Annotations;
using Zass.Core.Commands;

namespace Zass.Tests;

public class UndoRedoManagerTests
{
    private static PhysicalPoint P(double x, double y) => new(x, y);

    private static RectangleAnnotation Rect() => new(P(0, 0), P(10, 10));

    [Fact]
    public void Execute_AddsAnnotationAndEnablesUndo()
    {
        var layer = new AnnotationLayer();
        var manager = new UndoRedoManager();
        var rect = Rect();

        manager.Execute(new AddAnnotationCommand(layer, rect));

        Assert.Single(layer.Items);
        Assert.True(manager.CanUndo);
        Assert.False(manager.CanRedo);
    }

    [Fact]
    public void Undo_RevertsAddAndEnablesRedo()
    {
        var layer = new AnnotationLayer();
        var manager = new UndoRedoManager();
        var rect = Rect();
        manager.Execute(new AddAnnotationCommand(layer, rect));

        manager.Undo();

        Assert.Empty(layer.Items);
        Assert.False(manager.CanUndo);
        Assert.True(manager.CanRedo);
    }

    [Fact]
    public void Redo_ReappliesUndoneCommand()
    {
        var layer = new AnnotationLayer();
        var manager = new UndoRedoManager();
        var rect = Rect();
        manager.Execute(new AddAnnotationCommand(layer, rect));
        manager.Undo();

        manager.Redo();

        Assert.Single(layer.Items);
        Assert.Same(rect, layer.Items[0]);
    }

    [Fact]
    public void Execute_AfterUndo_ClearsRedoStack()
    {
        var layer = new AnnotationLayer();
        var manager = new UndoRedoManager();
        manager.Execute(new AddAnnotationCommand(layer, Rect()));
        manager.Undo();
        Assert.True(manager.CanRedo);

        manager.Execute(new AddAnnotationCommand(layer, Rect()));

        Assert.False(manager.CanRedo);
    }

    [Fact]
    public void RemoveCommand_Undo_RestoresOriginalZOrder()
    {
        var layer = new AnnotationLayer();
        var manager = new UndoRedoManager();
        var a = Rect();
        var b = Rect();
        var c = Rect();
        layer.Add(a);
        layer.Add(b);
        layer.Add(c);

        manager.Execute(new RemoveAnnotationCommand(layer, b));
        Assert.Equal(new[] { a, c }, layer.Items);

        manager.Undo();
        Assert.Equal(new[] { a, b, c }, layer.Items); // b back at index 1
    }

    [Fact]
    public void MoveCommand_ExecuteAndUndo_AreSymmetric()
    {
        var manager = new UndoRedoManager();
        var rect = Rect();

        manager.Execute(new MoveAnnotationCommand(rect, 25, -10));
        Assert.Equal(P(25, -10), rect.Start);
        Assert.Equal(P(35, 0), rect.End);

        manager.Undo();
        Assert.Equal(P(0, 0), rect.Start);
        Assert.Equal(P(10, 10), rect.End);

        manager.Redo();
        Assert.Equal(P(25, -10), rect.Start);
    }

    [Fact]
    public void UndoAndRedo_AreNoOpsWhenStacksEmpty()
    {
        var manager = new UndoRedoManager();

        manager.Undo();
        manager.Redo();

        Assert.False(manager.CanUndo);
        Assert.False(manager.CanRedo);
    }

    [Fact]
    public void Clear_DiscardsAllHistory()
    {
        var layer = new AnnotationLayer();
        var manager = new UndoRedoManager();
        manager.Execute(new AddAnnotationCommand(layer, Rect()));
        manager.Execute(new AddAnnotationCommand(layer, Rect()));

        manager.Clear();

        Assert.False(manager.CanUndo);
        Assert.False(manager.CanRedo);
        Assert.Equal(2, layer.Items.Count); // Clear drops history, not the annotations
    }

    [Fact]
    public void StateChanged_FiresOnExecuteUndoRedo()
    {
        var layer = new AnnotationLayer();
        var manager = new UndoRedoManager();
        int count = 0;
        manager.StateChanged += (_, _) => count++;

        manager.Execute(new AddAnnotationCommand(layer, Rect()));
        manager.Undo();
        manager.Redo();

        Assert.Equal(3, count);
    }

    [Fact]
    public void MultiStep_UndoRedoSequenceKeepsLayerConsistent()
    {
        var layer = new AnnotationLayer();
        var manager = new UndoRedoManager();
        var a = Rect();
        var b = Rect();

        manager.Execute(new AddAnnotationCommand(layer, a));
        manager.Execute(new AddAnnotationCommand(layer, b));
        manager.Execute(new MoveAnnotationCommand(a, 5, 5));

        manager.Undo(); // undo move
        manager.Undo(); // undo add b
        Assert.Equal(new[] { a }, layer.Items);
        Assert.Equal(P(0, 0), a.Start);

        manager.Redo(); // redo add b
        manager.Redo(); // redo move
        Assert.Equal(new[] { a, b }, layer.Items);
        Assert.Equal(P(5, 5), a.Start);
    }
}
