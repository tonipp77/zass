using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Zass.Core.Collage;

namespace Zass.App.Imaging;

internal static class CollageComposer
{
    /// <summary>Renders only original crops on white, without editor chrome or zoom.</summary>
    public static BitmapSource Compose(CollageDocument document, IReadOnlyDictionary<Guid, BitmapSource> images)
    {
        var bounds = document.Bounds;
        if (bounds.IsEmpty) throw new InvalidOperationException("The collage is empty.");
        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.NearestNeighbor);
        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, bounds.Width, bounds.Height));
            foreach (var item in document.ItemsInPaintOrder)
            {
                var r = item.Bounds;
                if (item.Decoration is null)
                    dc.DrawImage(images[item.Id], new Rect(r.X - bounds.X, r.Y - bounds.Y, r.Width, r.Height));
                else
                {
                    dc.PushTransform(new TranslateTransform(r.X - bounds.X, r.Y - bounds.Y));
                    dc.DrawDrawing(CollageDecorationRenderer.Draw(item));
                    dc.Pop();
                }
            }
        }
        var result = new RenderTargetBitmap(bounds.Width, bounds.Height, 96, 96, PixelFormats.Pbgra32);
        result.Render(visual);
        result.Freeze();
        return result;
    }
}
