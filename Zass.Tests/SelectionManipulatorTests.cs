using Zass.Core.Capture;
using Zass.Interop;

namespace Zass.Tests;

public class SelectionManipulatorTests
{
    private static readonly PhysicalRect Monitor = new(0, 0, 1920, 1080);
    private static readonly PhysicalRect Sel = new(100, 100, 200, 100); // L100 T100 R300 B200

    [Theory]
    [InlineData(100, 100, SelectionHandle.TopLeft)]
    [InlineData(200, 100, SelectionHandle.Top)]
    [InlineData(300, 100, SelectionHandle.TopRight)]
    [InlineData(300, 150, SelectionHandle.Right)]
    [InlineData(300, 200, SelectionHandle.BottomRight)]
    [InlineData(200, 200, SelectionHandle.Bottom)]
    [InlineData(100, 200, SelectionHandle.BottomLeft)]
    [InlineData(100, 150, SelectionHandle.Left)]
    public void HitTest_DetectsHandleAtItsCenter(int px, int py, SelectionHandle expected)
        => Assert.Equal(expected, SelectionManipulator.HitTest(Sel, px, py, 5));

    [Fact]
    public void HitTest_WithinRadiusStillHitsHandle()
        => Assert.Equal(SelectionHandle.TopLeft, SelectionManipulator.HitTest(Sel, 103, 97, 5));

    [Fact]
    public void HitTest_InteriorReturnsInside()
        => Assert.Equal(SelectionHandle.Inside, SelectionManipulator.HitTest(Sel, 200, 150, 5));

    [Fact]
    public void HitTest_FarAwayReturnsNone()
        => Assert.Equal(SelectionHandle.None, SelectionManipulator.HitTest(Sel, 1000, 1000, 5));

    [Fact]
    public void HitTest_EmptySelectionReturnsNone()
        => Assert.Equal(SelectionHandle.None, SelectionManipulator.HitTest(default, 0, 0, 5));

    // ---- Resize ----

    [Fact]
    public void Resize_BottomRightMovesOnlyRightAndBottom()
    {
        PhysicalRect r = SelectionManipulator.Resize(Sel, SelectionHandle.BottomRight, 400, 260, Monitor, 5);
        Assert.Equal(new PhysicalRect(100, 100, 300, 160), r);
    }

    [Fact]
    public void Resize_TopLeftMovesOnlyLeftAndTop()
    {
        PhysicalRect r = SelectionManipulator.Resize(Sel, SelectionHandle.TopLeft, 50, 40, Monitor, 5);
        Assert.Equal(new PhysicalRect(50, 40, 250, 160), r);
    }

    [Fact]
    public void Resize_LeftEdgeOnlyChangesLeft()
    {
        PhysicalRect r = SelectionManipulator.Resize(Sel, SelectionHandle.Left, 150, 999, Monitor, 5);
        Assert.Equal(new PhysicalRect(150, 100, 150, 100), r);
    }

    [Fact]
    public void Resize_ClampsMovingEdgeToBounds()
    {
        PhysicalRect r = SelectionManipulator.Resize(Sel, SelectionHandle.TopLeft, -500, -500, Monitor, 5);
        // Left/top clamp to the monitor origin; right/bottom unchanged.
        Assert.Equal(new PhysicalRect(0, 0, 300, 200), r);
    }

    [Fact]
    public void Resize_EnforcesMinSizeInsteadOfFlipping()
    {
        // Drag the right edge far past the left edge: it stops minSize away from left.
        PhysicalRect r = SelectionManipulator.Resize(Sel, SelectionHandle.Right, 0, 150, Monitor, 5);
        Assert.Equal(new PhysicalRect(100, 100, 5, 100), r);
    }

    [Fact]
    public void Resize_TopEnforcesMinSizeAgainstBottom()
    {
        PhysicalRect r = SelectionManipulator.Resize(Sel, SelectionHandle.Top, 999, 999, Monitor, 5);
        // Top cannot pass bottom(200) - minSize(5) = 195.
        Assert.Equal(new PhysicalRect(100, 195, 200, 5), r);
    }

    // ---- Move ----

    [Fact]
    public void Move_TranslatesWithinBounds()
    {
        PhysicalRect r = SelectionManipulator.Move(Sel, 50, -30, Monitor);
        Assert.Equal(new PhysicalRect(150, 70, 200, 100), r);
    }

    [Fact]
    public void Move_ClampsAgainstTopLeftCorner()
    {
        PhysicalRect r = SelectionManipulator.Move(Sel, -1000, -1000, Monitor);
        Assert.Equal(new PhysicalRect(0, 0, 200, 100), r);
    }

    [Fact]
    public void Move_ClampsAgainstBottomRightCorner()
    {
        PhysicalRect r = SelectionManipulator.Move(Sel, 100000, 100000, Monitor);
        Assert.Equal(new PhysicalRect(1720, 980, 200, 100), r);
    }

    [Fact]
    public void Move_RespectsNonZeroMonitorOrigin()
    {
        var monitor = new PhysicalRect(-1920, 0, 1920, 1080);
        var sel = new PhysicalRect(-1900, 50, 200, 100);
        PhysicalRect r = SelectionManipulator.Move(sel, -1000, 0, monitor);
        Assert.Equal(new PhysicalRect(-1920, 50, 200, 100), r);
    }
}
