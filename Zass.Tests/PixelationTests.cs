using Zass.Core.Annotations;
using Zass.Core.Commands;
using Zass.Interop;

namespace Zass.Tests;

public class PixelationTests
{
    [Fact]
    public void BlocksReplaceAllDetail_AndIncludePartialEdgesWithoutTouchingStridePadding()
    {
        // A 3x2 image with four padding bytes per row and a one-pixel-wide edge block.
        byte[] pixels = [0,0,0,255, 100,100,100,255, 20,20,20,255, 9,9,9,9,
                         100,100,100,255, 200,200,200,255, 60,60,60,255, 8,8,8,8];
        PixelationEffect.Apply(pixels, 3, 2, 16, 2);
        foreach (int i in new[] { 0, 4, 16, 20 })
            Assert.Equal(new byte[] {100,100,100,255}, pixels[i..(i+4)]);
        foreach (int i in new[] { 8, 24 })
            Assert.Equal(new byte[] {40,40,40,255}, pixels[i..(i+4)]);
        Assert.Equal(new byte[] {9,9,9,9}, pixels[12..16]);
        Assert.Equal(new byte[] {8,8,8,8}, pixels[28..32]);
    }

    [Fact]
    public void LargerThanRegionBlock_StillReplacesEveryPixel()
    {
        byte[] pixels = [0,0,0,255, 200,200,200,255];
        PixelationEffect.Apply(pixels, 2, 1, 8, 64);
        Assert.Equal(new byte[] {100,100,100,255,100,100,100,255}, pixels);
    }

    [Fact]
    public void InvalidBuffer_IsRejectedBeforeMutation()
    {
        byte[] pixels = [1,2,3,255];
        Assert.Throws<ArgumentOutOfRangeException>(() => PixelationEffect.Apply(pixels, 2, 1, 8, 8));
        Assert.Equal(new byte[] {1,2,3,255}, pixels);
    }

    [Fact]
    public void RectangleNormalizes_AndWholeObjectSurvivesUndoRedo()
    {
        var effect = new PixelationAnnotation(new(50, 60), new(10, 20)) { BlockSize = 32 };
        Assert.Equal(new PhysicalRect(10, 20, 40, 40), effect.Bounds);
        Assert.True(effect.HitTest(new(30, 40)));
        Assert.False(effect.HitTest(new(100, 100)));
        var layer = new AnnotationLayer();
        var history = new UndoRedoManager();
        history.Execute(new AddAnnotationCommand(layer, effect));
        history.Undo();
        Assert.Empty(layer.Items);
        history.Redo();
        Assert.Same(effect, Assert.Single(layer.Items));
        Assert.Equal(32, effect.BlockSize);
    }
}
