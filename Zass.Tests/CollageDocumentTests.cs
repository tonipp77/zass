using Zass.Core.Collage;
using Zass.Interop;

namespace Zass.Tests;

public class CollageDocumentTests
{
    [Fact]
    public void CropsArrowsTextAndMovement_ShareChronologicalHistory()
    {
        var doc = new CollageDocument();
        Guid crop = doc.Add(100, 50);
        var arrow = new CollageDecoration(CollageDecorationKind.CurvedArrow, new(0, 0), new(80, 40),
            Zass.Core.Annotations.ArgbColor.Red, 16);
        Guid arrowId = doc.AddDecoration(arrow, new PhysicalRect(110, 10, 90, 50));
        var text = arrow with { Kind = CollageDecorationKind.Text, Text = "Step 1" };
        Guid textId = doc.AddDecoration(text, new PhysicalRect(10, 70, 100, 24));
        doc.Move(arrowId, 120, 20);
        doc.Undo();
        Assert.Equal(110, doc.Items[1].Bounds.X);
        doc.Undo();
        Assert.DoesNotContain(doc.Items, i => i.Id == textId);
        doc.Undo();
        Assert.Equal(crop, Assert.Single(doc.Items).Id);
        doc.Redo(); doc.Redo(); doc.Redo();
        Assert.Equal(arrow, doc.Items[1].Decoration);
        Assert.Equal("Step 1", doc.Items[2].Decoration!.Text);
        Assert.Equal(120, doc.Items[1].Bounds.X);
        doc.Clear(); doc.Undo();
        Assert.Equal(3, doc.Items.Count);
        Assert.Equal(new PhysicalRect(0, 0, 210, 94), doc.Bounds);
    }

    [Fact]
    public void FreePlacement_UsesOriginalSizesAndTightExportBounds()
    {
        var doc = new CollageDocument();
        Guid first = doc.Add(100, 50);
        Guid second = doc.Add(80, 60);
        doc.Move(first, 20, 30);
        doc.Move(second, 140, 40);
        Assert.Equal(new PhysicalRect(20, 30, 200, 70), doc.Bounds);
        Assert.Equal(100, doc.Items[0].Bounds.Width);
        Assert.Equal(60, doc.Items[1].Bounds.Height);
    }

    [Fact]
    public void AddMoveRemoveClear_UndoRedoRestoresOrderAndGeometry()
    {
        var doc = new CollageDocument();
        Guid first = doc.Add(100, 50);
        Guid second = doc.Add(80, 60);
        doc.Move(second, 5, 10);
        doc.Remove(first);
        doc.Clear();
        Assert.Empty(doc.Items);
        doc.Undo();
        doc.Undo();
        Assert.Equal(first, doc.Items[0].Id);
        Assert.Equal(new PhysicalRect(5, 10, 80, 60), doc.Items[1].Bounds);
        doc.Undo();
        Assert.Equal(50, doc.Items[1].Bounds.Y);
        doc.Redo();
        Assert.Equal(10, doc.Items[1].Bounds.Y);
        doc.Remove(second);
        Assert.False(doc.CanRedo);
    }

    [Fact]
    public void InvalidOrOversizedChanges_LeaveDocumentAndHistoryUntouched()
    {
        var doc = new CollageDocument();
        Guid id = doc.Add(100, 100);
        Assert.False(doc.CanMove(id, -1, 0));
        Assert.False(doc.CanMove(id, int.MaxValue, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => doc.Add(20_000, 20_000));
        Assert.Throws<ArgumentOutOfRangeException>(() => doc.Add(0, 10));
        Assert.Single(doc.Items);
        doc.Undo();
        Assert.Empty(doc.Items);
    }
}
