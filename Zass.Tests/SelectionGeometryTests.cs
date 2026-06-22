using Zass.Core.Capture;
using Zass.Interop;

namespace Zass.Tests;

public class SelectionGeometryTests
{
    [Theory]
    [InlineData(10, 20, 110, 220, 10, 20, 100, 200)] // top-left -> bottom-right
    [InlineData(110, 220, 10, 20, 10, 20, 100, 200)] // bottom-right -> top-left
    [InlineData(110, 20, 10, 220, 10, 20, 100, 200)] // top-right -> bottom-left
    [InlineData(10, 220, 110, 20, 10, 20, 100, 200)] // bottom-left -> top-right
    public void Normalize_HandlesAnyDragDirection(int x1, int y1, int x2, int y2,
        int ex, int ey, int ew, int eh)
    {
        PhysicalRect r = SelectionGeometry.Normalize(x1, y1, x2, y2);
        Assert.Equal(new PhysicalRect(ex, ey, ew, eh), r);
    }

    [Fact]
    public void Normalize_ZeroDragProducesEmptyRect()
    {
        PhysicalRect r = SelectionGeometry.Normalize(50, 50, 50, 50);
        Assert.True(r.IsEmpty);
    }

    [Fact]
    public void ClampToBounds_KeepsSelectionInsideMonitor()
    {
        var bounds = new PhysicalRect(0, 0, 1920, 1080);
        var selection = new PhysicalRect(-50, -20, 200, 200);

        PhysicalRect clamped = SelectionGeometry.ClampToBounds(selection, bounds);

        Assert.Equal(new PhysicalRect(0, 0, 150, 180), clamped);
    }

    [Fact]
    public void ClampToBounds_TrimsOverflowOnBottomRight()
    {
        var bounds = new PhysicalRect(0, 0, 1920, 1080);
        var selection = new PhysicalRect(1900, 1000, 200, 200);

        PhysicalRect clamped = SelectionGeometry.ClampToBounds(selection, bounds);

        Assert.Equal(new PhysicalRect(1900, 1000, 20, 80), clamped);
    }

    [Fact]
    public void ClampToBounds_RespectsNonZeroMonitorOrigin()
    {
        // Secondary-monitor style origin in virtual-desktop coordinates.
        var bounds = new PhysicalRect(-1920, 0, 1920, 1080);
        var selection = new PhysicalRect(-2000, -10, 300, 300);

        PhysicalRect clamped = SelectionGeometry.ClampToBounds(selection, bounds);

        Assert.Equal(new PhysicalRect(-1920, 0, 220, 290), clamped);
    }

    [Theory]
    [InlineData(5, 5, true)]
    [InlineData(4, 100, false)]
    [InlineData(100, 4, false)]
    [InlineData(0, 0, false)]
    public void IsValidSize_EnforcesMinimum(int w, int h, bool expected)
    {
        var selection = new PhysicalRect(0, 0, w, h);
        Assert.Equal(expected, SelectionGeometry.IsValidSize(selection));
    }
}
