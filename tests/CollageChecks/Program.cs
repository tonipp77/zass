using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Zass.App.Views;
using Zass.Core.Annotations;
using Zass.Core.Collage;
using Zass.Core.Export;
using Zass.Interop;

internal static class Program
{
    private static readonly Type Renderer = typeof(CollageWindow).Assembly.GetType("Zass.App.Imaging.CollageDecorationRenderer")!;
    private static readonly Type Composer = typeof(CollageWindow).Assembly.GetType("Zass.App.Imaging.CollageComposer")!;
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static CollageItem Create(CollageDecoration definition) => (CollageItem)Renderer.GetMethod("CreateItem")!.Invoke(null, [definition])!;
    private static Drawing Draw(CollageItem item) => (Drawing)Renderer.GetMethod("Draw")!.Invoke(null, [item])!;
    private static BitmapSource Compose(CollageDocument document) => (BitmapSource)Composer.GetMethod("Compose")!.Invoke(null, [document, new Dictionary<Guid, BitmapSource>()])!;
    private static byte[] Pixels(BitmapSource bitmap)
    {
        byte[] result = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyPixels(result, bitmap.PixelWidth * 4, 0); return result;
    }
    private static byte[] Preview(CollageItem item)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, item.Bounds.Width, item.Bounds.Height));
            dc.DrawImage(new DrawingImage(Draw(item)), new Rect(0, 0, item.Bounds.Width, item.Bounds.Height));
        }
        var bitmap = new RenderTargetBitmap(item.Bounds.Width, item.Bounds.Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual); return Pixels(bitmap);
    }
    private static void CheckDefinition(CollageDecoration definition)
    {
        var item = Create(definition);
        Check(item.Bounds.Width > 0 && item.Bounds.Height > 0, "Nonempty bounds");
        var doc = new CollageDocument();
        var id = doc.AddDecoration(item.Decoration!, item.Bounds);
        byte[] original = Pixels(Compose(doc));
        Check(original.Where((_, i) => i % 4 != 3).Any(b => b < 245), "Visible content");
        Check(original.SequenceEqual(Preview(item)), "Preview/export pixel equality");
        doc.Move(id, item.Bounds.X + 20, item.Bounds.Y + 30);
        Check(original.SequenceEqual(Pixels(Compose(doc))), "Movement preserves rendering");
        doc.Remove(id); doc.Undo(); doc.Undo();
        Check(original.SequenceEqual(Pixels(Compose(doc))), "Undo preserves style");
        doc.Undo(); Check(doc.Items.Count == 0, "Undo removes whole object");
        doc.Redo(); Check(original.SequenceEqual(Pixels(Compose(doc))), "Redo restores style");
        var shadow = Create(definition with { HasShadow = true });
        var plain = Create(definition with { HasShadow = false });
        Check(shadow.Bounds.Right > plain.Bounds.Right && shadow.Bounds.Bottom > plain.Bounds.Bottom, "Shadow extends export bounds");
    }
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target)!;
    private static void Click(CollageWindow window, string name) => ((ButtonBase)window.FindName(name)).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    [STAThread]
    private static void Main()
    {
        try { Run(); }
        catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
    }
    private static void CheckLayerOrder()
    {
        var window = new CollageWindow(() => (ImageExportFormat.Png, 90));
        var doc = Field<CollageDocument>(window, "_document");
        var mark = Create(new CollageDecoration(CollageDecorationKind.Stamp, new(30,30), new(30,30),
            ArgbColor.Red, 30, Stamp: CollageStamp.Plus));
        Guid markId = doc.AddDecoration(mark.Decoration!, mark.Bounds);
        var white = BitmapSource.Create(120,120,96,96,PixelFormats.Bgra32,null,
            Enumerable.Repeat((byte)255,120*120*4).ToArray(),120*4);
        window.AddCrop(white);
        Guid cropId = doc.Items.Last().Id;
        doc.Move(cropId, 0, 0);
        window.GetType().GetMethod("Refresh", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window,null);
        var children = ((Canvas)window.FindName("Surface")).Children.OfType<Image>().ToArray();
        Check((Guid)children[0].Tag == cropId && (Guid)children[1].Tag == markId, "Preview puts late crop below mark");
        var images = Field<Dictionary<Guid, BitmapSource>>(window, "_images");
        var bitmap = (BitmapSource)Composer.GetMethod("Compose")!.Invoke(null,[doc,images])!;
        var pixels = Pixels(bitmap);
        Check(Enumerable.Range(0,pixels.Length/4).Any(i => pixels[4*i+2]>180 && pixels[4*i]<60),
            "Export preserves mark above opaque late crop");
        doc.Undo(); doc.Redo();
        Check(Pixels((BitmapSource)Composer.GetMethod("Compose")!.Invoke(null,[doc,images])!).SequenceEqual(pixels),
            "Layer order survives undo/redo");
        window.CloseForShutdown();
    }

    private static void Run()
    {
        CheckLayerOrder();
        int count = 0;
        var template = new CollageDecoration(CollageDecorationKind.Text, new PhysicalPoint(60,60), new PhysicalPoint(240,170),
            ArgbColor.Red, 26, "Paso A", HasShadow: true, Opacity: 0.65, Bold: true, Italic: true, TextOutline: true);
        foreach (var stamp in Enum.GetValues<CollageStamp>()) { CheckDefinition(template with { Kind = CollageDecorationKind.Stamp, Stamp = stamp }); count++; }
        foreach (var shape in Enum.GetValues<CollageBadgeShape>()) { CheckDefinition(template with { Kind = CollageDecorationKind.Step, BadgeShape = shape, Text = "12" }); count++; }
        foreach (var shape in Enum.GetValues<CollageLabelShape>()) { CheckDefinition(template with { Kind = CollageDecorationKind.Label, LabelShape = shape }); count++; }
        foreach (string font in new[] { "Segoe UI", "Arial", "Calibri", "Consolas", "Georgia" }) { CheckDefinition(template with { FontFamily = font }); count++; }
        foreach (var kind in new[] { CollageDecorationKind.CurvedArrow, CollageDecorationKind.StraightArrow, CollageDecorationKind.ElbowArrow })
        foreach (var tip in Enum.GetValues<CollageArrowTip>())
        foreach (bool dashed in new[] { false, true })
        foreach (var end in new[] { new PhysicalPoint(240,170), new PhysicalPoint(10,10), new PhysicalPoint(60,240), new PhysicalPoint(240,60) })
        { CheckDefinition(template with { Kind = kind, ArrowTip = tip, Dashed = dashed, To = end }); count++; }
        var regular = template with { Text = "HOLA", Bold = false, Italic = false, Size = 96, HasShadow = false, Opacity = 1 };
        int Colored(byte[] pixels) => Enumerable.Range(0, pixels.Length / 4)
            .Count(i => pixels[i*4+2] > 180 && pixels[i*4+1] < 60 && pixels[i*4] < 60);
        int plainColor = Colored(Preview(Create(regular with { TextOutline = false })));
        int outlinedColor = Colored(Preview(Create(regular with { TextOutline = true })));
        Check(plainColor > 0 && outlinedColor >= plainColor * 0.95, "Outline preserves regular-weight colored fill");
        var window = new CollageWindow(() => (ImageExportFormat.Png, 90));
        var bitmap = BitmapSource.Create(40,30,96,96,PixelFormats.Bgra32,null,new byte[40*30*4],40*4);
        window.RetainCapture(bitmap); window.RetainCapture(bitmap);
        var strip = (StackPanel)window.FindName("CaptureStrip");
        var doc = Field<CollageDocument>(window,"_document");
        Check(strip.Children.Count == 1 && doc.Items.Count == 0,"Retain without placement");
        ((Button)strip.Children[0]).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        ((Button)strip.Children[0]).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Check(doc.Items.Count == 2 && strip.Children.Count == 1,"Reinsert capture");
        Click(window,"ClearButton"); Click(window,"UndoButton"); Check(doc.Items.Count == 2,"Clear undo");
        foreach(string tool in new[]{"StampTool","StepTool","LabelTool","TextTool"})
        {
            Click(window,tool);
            Check(((StackPanel)window.FindName("ArrowStyles")).Visibility == Visibility.Collapsed,"Hide irrelevant arrow options");
        }
        Click(window,"StepTool");
        window.GetType().GetMethod("BeginText",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(window,[new PhysicalPoint(80,80)]);
        Field<TextBox>(window,"_textEditor").Text="B";
        window.PrepareForCapture();
        Check(doc.Items.Last().Decoration?.Kind == CollageDecorationKind.Step && doc.Items.Last().Decoration?.Text == "B","Inline step commits as step");
        Check(((ToggleButton)window.FindName("PointerTool")).IsChecked == true, "New object ready for moving");
        var beforeMove = doc.Items.Last();
        window.GetType().GetMethod("NudgeSelected", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(window, [System.Windows.Input.Key.Right, 10]);
        Check(doc.Items.Last().Bounds.X == beforeMove.Bounds.X + 10, "Keyboard moves selected object");
        Click(window, "UndoButton"); Check(doc.Items.Last().Bounds == beforeMove.Bounds, "Undo keyboard movement");
        var beforeZoom = doc.Items.ToArray();
        Click(window, "ZoomActualButton");
        Check(((Slider)window.FindName("ZoomSlider")).Value == 1, "Actual size zoom");
        Click(window, "ZoomOutButton");
        Check(((Slider)window.FindName("ZoomSlider")).Value < 1, "Zoom out");
        Click(window, "ZoomInButton");
        Check(doc.Items.SequenceEqual(beforeZoom), "Zoom leaves model untouched");
        var root=(FrameworkElement)window.Content;
        foreach(var size in new[]{new Size(900,580),new Size(1280,820)})
        {
            root.Measure(size); root.Arrange(new Rect(size)); root.UpdateLayout();
            Check(root.DesiredSize.Width<=size.Width && root.DesiredSize.Height<=size.Height,"Layout fits");
        }
        window.CloseForShutdown(); Check(strip.Children.Count==0,"Shutdown releases captures");
        Console.WriteLine($"PASS: {count} style variants; preview/export equality, bounds, history, editor and buffer checks.");
    }
}
