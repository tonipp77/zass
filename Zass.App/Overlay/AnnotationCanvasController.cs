using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Media.Imaging;
using Zass.App.Imaging;
using Zass.Core.Annotations;
using Zass.Core.Commands;
using Zass.Interop;

namespace Zass.App.Overlay;

/// <summary>
/// Presentation-layer bridge between the pure annotation model (<see cref="Zass.Core"/>)
/// and the WPF <see cref="Canvas"/>. It materializes each model annotation as a
/// WPF element, drives the live drawing draft, hosts the inline text editor, and
/// routes undo/redo through the <see cref="UndoRedoManager"/>. The model stays
/// UI-free; this is the single place that knows about WPF (see Arquitectura §6.1).
/// </summary>
internal sealed class AnnotationCanvasController
{
    /// <summary>Below this side/length (physical px) a drag is treated as an accidental click.</summary>
    private const double MinAnnotationSizePhysical = 3.0;

    /// <summary>Minimum spacing (physical px) between consecutive freehand points.</summary>
    private const double FreehandPointSpacingPhysical = 1.5;

    private readonly Canvas _canvas;
    private readonly AnnotationLayer _layer = new();
    private readonly UndoRedoManager _history = new();
    private readonly Func<PhysicalPoint, Point> _toDip;
    private readonly double _scale;
    private readonly Dictionary<Annotation, FrameworkElement> _visuals = new();
    private readonly Dictionary<Annotation, BitmapSource> _pixelationImages = new();
    private BitmapSource? _pixelationSource;
    public Func<BitmapSource>? CaptureUnderlay { get; set; }

    // In-progress drag annotation (rectangle/filled/arrow/freehand) not yet committed.
    private Annotation? _draft;

    // In-progress inline text editor; non-null only while the user is typing.
    private TextBox? _editingTextBox;
    private PhysicalPoint _editPosition;
    private ArgbColor _editColor;
    private double _editFontSize;
    private bool _editShadow;

    public AnnotationCanvasController(Canvas canvas, Func<PhysicalPoint, Point> toDip, double scale)
    {
        _canvas = canvas;
        _toDip = toDip;
        _scale = scale;
    }

    /// <summary>Raised after an inline text edit finishes, so the host can take keyboard focus back.</summary>
    public event Action? TextEditCompleted;

    public bool CanUndo => _history.CanUndo;

    public bool CanRedo => _history.CanRedo;

    public bool HasAnnotations => _layer.Items.Count > 0;

    /// <summary>True while the inline text editor has focus (suppresses tool shortcuts in the host).</summary>
    public bool IsEditingText => _editingTextBox is not null;

    // --- Drag-drawn annotations (rectangle, filled rectangle, arrow, freehand) ---

    /// <summary>Begins a draft annotation for a drag-drawn tool and shows it live on the canvas.</summary>
    public void BeginDraft(OverlayTool tool, PhysicalPoint start, ArgbColor color, double thicknessPhysical, bool hasShadow = false, ArrowStyle arrowStyle = ArrowStyle.Triangular)
    {
        // Snapshot only confirmed content, before adding the pixelation draft to the canvas.
        _pixelationSource = tool == OverlayTool.Pixelation ? CaptureUnderlay?.Invoke() : null;
        Annotation annotation = tool switch
        {
            OverlayTool.Rectangle => new RectangleAnnotation(start, start) { Thickness = thicknessPhysical },
            OverlayTool.FilledRectangle => new FilledRectangleAnnotation(start, start),
            OverlayTool.Arrow => new ArrowAnnotation(start, start) { Thickness = thicknessPhysical },
            OverlayTool.Line => new LineAnnotation(start, start) { Thickness = thicknessPhysical },
            OverlayTool.Pixelation => new PixelationAnnotation(start, start) { BlockSize = (int)thicknessPhysical },
            OverlayTool.Freehand => new FreehandAnnotation(new[] { start }) { Thickness = thicknessPhysical },
            _ => throw new NotSupportedException($"{tool} is not a drag-drawn tool."),
        };
        annotation.Color = color;
        annotation.HasShadow = hasShadow && annotation is not PixelationAnnotation;
        if (annotation is ArrowAnnotation arrow) arrow.Style = arrowStyle;

        _draft = annotation;
        FrameworkElement visual = CreateVisual(annotation);
        _visuals[annotation] = visual;
        Panel.SetZIndex(visual, int.MaxValue);
        _canvas.Children.Add(visual);
        UpdateVisual(visual, annotation);
    }

    /// <summary>Extends the current draft to follow the cursor.</summary>
    public void UpdateDraft(PhysicalPoint current)
    {
        if (_draft is null)
        {
            return;
        }

        switch (_draft)
        {
            case RectangleAnnotation r:
                r.End = current;
                break;
            case FilledRectangleAnnotation fr:
                fr.End = current;
                break;
            case ArrowAnnotation ar:
                ar.To = current;
                break;
            case LineAnnotation ln:
                ln.To = current;
                break;
            case PixelationAnnotation px:
                px.End = current;
                break;
            case FreehandAnnotation fh:
                PhysicalPoint last = fh.Points[^1];
                if (current.DistanceTo(last) >= FreehandPointSpacingPhysical)
                {
                    fh.AddPoint(current);
                }

                break;
        }

        UpdateVisual(_visuals[_draft], _draft);
    }

    /// <summary>
    /// Commits the current draft as an undoable command, or discards it when the
    /// drag was too small to be intentional. Returns true when something was added.
    /// </summary>
    public bool CommitDraft()
    {
        if (_draft is null)
        {
            return false;
        }

        Annotation annotation = _draft;
        _draft = null;
        _pixelationSource = null;

        if (!IsDraftMeaningful(annotation))
        {
            _pixelationImages.Remove(annotation);
            if (_visuals.Remove(annotation, out FrameworkElement? visual))
            {
                _canvas.Children.Remove(visual);
            }

            return false;
        }

        // The visual already exists and is registered; Execute adds the model and
        // Resync just restores the proper z-order.
        _history.Execute(new AddAnnotationCommand(_layer, annotation));
        PrunePixelationImages();
        Resync();
        return true;
    }

    private static bool IsDraftMeaningful(Annotation annotation) => annotation switch
    {
        RectangleAnnotation r => HasMinSpan(r.Start, r.End),
        FilledRectangleAnnotation fr => HasMinSpan(fr.Start, fr.End),
        ArrowAnnotation ar => ar.From.DistanceTo(ar.To) >= MinAnnotationSizePhysical,
        LineAnnotation ln => ln.From.DistanceTo(ln.To) >= MinAnnotationSizePhysical,
        PixelationAnnotation px => HasMinSpan(px.Start, px.End),
        FreehandAnnotation fh => fh.Points.Count >= 2 && PathLength(fh.Points) >= MinAnnotationSizePhysical,
        _ => false,
    };

    private static bool HasMinSpan(PhysicalPoint a, PhysicalPoint b) =>
        Math.Abs(b.X - a.X) >= MinAnnotationSizePhysical && Math.Abs(b.Y - a.Y) >= MinAnnotationSizePhysical;

    private static double PathLength(IReadOnlyList<PhysicalPoint> points)
    {
        double total = 0;
        for (int i = 1; i < points.Count; i++)
        {
            total += points[i].DistanceTo(points[i - 1]);
        }

        return total;
    }

    // --- Inline text annotation ---

    /// <summary>Places an inline text editor at <paramref name="position"/> and gives it focus.</summary>
    public void BeginText(PhysicalPoint position, ArgbColor color, double fontSizePhysical, bool hasShadow = false)
    {
        if (IsEditingText)
        {
            CommitText();
        }

        Point dip = _toDip(position);
        SolidColorBrush brush = Brush(color);
        var box = new TextBox
        {
            MinWidth = 4,
            Background = Brushes.Transparent,
            Foreground = brush,
            CaretBrush = brush,
            BorderThickness = new Thickness(0),
            BorderBrush = Brushes.Transparent,
            Padding = new Thickness(0),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = fontSizePhysical / _scale,
            AcceptsReturn = false,
            AcceptsTab = false,
            SnapsToDevicePixels = true,
        };
        Canvas.SetLeft(box, dip.X);
        Canvas.SetTop(box, dip.Y);
        Panel.SetZIndex(box, int.MaxValue);
        box.PreviewKeyDown += OnTextBoxPreviewKeyDown;
        box.LostKeyboardFocus += OnTextBoxLostKeyboardFocus;
        _canvas.Children.Add(box);

        _editingTextBox = box;
        _editPosition = position;
        _editColor = color;
        _editFontSize = fontSizePhysical;
        _editShadow = hasShadow;
        box.Effect = CreateShadow(hasShadow);

        // Defer focus to Input priority: the click that placed the box is still being
        // processed and the input manager would otherwise hand focus back to the window.
        box.Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Input,
            new Action(() =>
            {
                if (_editingTextBox == box)
                {
                    box.Focus();
                    Keyboard.Focus(box);
                }
            }));
    }

    /// <summary>
    /// Finishes the inline text edit: commits a <see cref="TextAnnotation"/> when the
    /// box has content, or discards it when empty. Safe to call when not editing.
    /// </summary>
    public void CommitText()
    {
        if (_editingTextBox is null)
        {
            return;
        }

        TextBox box = _editingTextBox;
        _editingTextBox = null; // Clear first so the LostFocus handler does not re-enter.
        box.PreviewKeyDown -= OnTextBoxPreviewKeyDown;
        box.LostKeyboardFocus -= OnTextBoxLostKeyboardFocus;
        _canvas.Children.Remove(box);

        if (!string.IsNullOrWhiteSpace(box.Text))
        {
            var annotation = new TextAnnotation(_editPosition, box.Text, _editFontSize) { Color = _editColor, HasShadow = _editShadow };
            _history.Execute(new AddAnnotationCommand(_layer, annotation));
            PrunePixelationImages();
            Resync();
        }

        TextEditCompleted?.Invoke();
    }

    private void OnTextBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Enter and Esc both confirm the text as an object (RF-13). Esc here does
        // not cancel the overlay because the editor has keyboard focus.
        if (e.Key is Key.Return or Key.Escape)
        {
            e.Handled = true;
            CommitText();
        }
    }

    private void OnTextBoxLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => CommitText();

    // --- Undo / redo ---

    public void Undo()
    {
        _history.Undo();
        Resync();
    }

    public void Redo()
    {
        _history.Redo();
        Resync();
    }

    private void PrunePixelationImages()
    {
        // A new command discards redo; release raster effects that only existed in that branch.
        foreach (Annotation annotation in _pixelationImages.Keys.Where(a => !_layer.Items.Contains(a)).ToArray())
            _pixelationImages.Remove(annotation);
    }

    /// <summary>Reconciles the canvas elements with the current model state and z-order.</summary>
    private void Resync()
    {
        var live = new HashSet<Annotation>(_layer.Items);
        foreach (KeyValuePair<Annotation, FrameworkElement> entry in
                 _visuals.Where(e => e.Key != _draft && !live.Contains(e.Key)).ToList())
        {
            _canvas.Children.Remove(entry.Value);
            _visuals.Remove(entry.Key);
        }

        for (int i = 0; i < _layer.Items.Count; i++)
        {
            Annotation annotation = _layer.Items[i];
            if (!_visuals.TryGetValue(annotation, out FrameworkElement? visual))
            {
                visual = CreateVisual(annotation);
                _visuals[annotation] = visual;
                _canvas.Children.Add(visual);
            }

            UpdateVisual(visual, annotation);
            Panel.SetZIndex(visual, i);
        }
    }

    private static FrameworkElement CreateVisual(Annotation annotation) => annotation switch
    {
        // Annotations don't intercept the mouse in this increment; the pointer tool
        // still drives the selection. Per-annotation hit-testing arrives later.
        RectangleAnnotation => new Rectangle { IsHitTestVisible = false },
        FilledRectangleAnnotation => new Rectangle { IsHitTestVisible = false },
        ArrowAnnotation => new Path { IsHitTestVisible = false },
        LineAnnotation => new Line { IsHitTestVisible = false },
        PixelationAnnotation => new Image { IsHitTestVisible = false, Stretch = Stretch.Fill },
        FreehandAnnotation => new Polyline
        {
            IsHitTestVisible = false,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
        },
        TextAnnotation => new TextBlock { IsHitTestVisible = false, FontFamily = new FontFamily("Segoe UI") },
        _ => throw new NotSupportedException($"No renderer for {annotation.GetType().Name}."),
    };

    private void UpdateVisual(FrameworkElement visual, Annotation annotation)
    {
        visual.Effect = CreateShadow(annotation.HasShadow && annotation is not PixelationAnnotation);
        switch (annotation)
        {
            case PixelationAnnotation px:
                var pixelImage = (Image)visual;
                var bounds = px.Bounds;
                Point origin = _toDip(new PhysicalPoint(bounds.X, bounds.Y));
                Canvas.SetLeft(pixelImage, origin.X);
                Canvas.SetTop(pixelImage, origin.Y);
                pixelImage.Width = bounds.Width / _scale;
                pixelImage.Height = bounds.Height / _scale;
                RenderOptions.SetBitmapScalingMode(pixelImage, BitmapScalingMode.NearestNeighbor);
                if (_draft == annotation && _pixelationSource is not null && !bounds.IsEmpty)
                {
                    int x = (int)Math.Round(origin.X * _scale), y = (int)Math.Round(origin.Y * _scale);
                    var region = new Int32Rect(x, y, bounds.Width, bounds.Height);
                    _pixelationImages[annotation] = PixelationRenderer.Create(_pixelationSource, region, px.BlockSize);
                }
                if (_pixelationImages.TryGetValue(annotation, out var bitmap)) pixelImage.Source = bitmap;
                break;

            case RectangleAnnotation r:
                var outline = (Rectangle)visual;
                PlaceRectangle(outline, r.Start, r.End);
                outline.Stroke = Brush(r.Color);
                outline.StrokeThickness = r.Thickness / _scale;
                outline.Fill = null;
                break;

            case FilledRectangleAnnotation fr:
                var filled = (Rectangle)visual;
                PlaceRectangle(filled, fr.Start, fr.End);
                filled.Fill = Brush(fr.Color);
                filled.Stroke = null;
                break;

            case ArrowAnnotation ar:
                var path = (Path)visual;
                double arrowThicknessDip = ar.Thickness / _scale;
                path.Data = BuildArrowGeometry(_toDip(ar.From), _toDip(ar.To), arrowThicknessDip, ar.Style);
                path.Stroke = ar.Style == ArrowStyle.Open ? Brush(ar.Color) : null;
                path.Fill = ar.Style == ArrowStyle.Open ? null : Brush(ar.Color);
                path.StrokeThickness = arrowThicknessDip;
                path.StrokeStartLineCap = PenLineCap.Flat;
                path.StrokeEndLineCap = PenLineCap.Flat;
                path.StrokeLineJoin = PenLineJoin.Miter;
                break;

            case LineAnnotation ln:
                var line = (Line)visual;
                Point from = _toDip(ln.From);
                Point to = _toDip(ln.To);
                line.X1 = from.X;
                line.Y1 = from.Y;
                line.X2 = to.X;
                line.Y2 = to.Y;
                line.Stroke = Brush(ln.Color);
                line.StrokeThickness = ln.Thickness / _scale;
                line.StrokeStartLineCap = PenLineCap.Round;
                line.StrokeEndLineCap = PenLineCap.Round;
                break;

            case FreehandAnnotation fh:
                var polyline = (Polyline)visual;
                var points = new PointCollection(fh.Points.Count);
                foreach (PhysicalPoint p in fh.Points)
                {
                    points.Add(_toDip(p));
                }

                polyline.Points = points;
                polyline.Stroke = Brush(fh.Color);
                polyline.StrokeThickness = fh.Thickness / _scale;
                break;

            case TextAnnotation t:
                var label = (TextBlock)visual;
                label.Text = t.Text;
                label.FontSize = t.FontSize / _scale;
                label.Foreground = Brush(t.Color);
                Point dip = _toDip(t.Position);
                Canvas.SetLeft(label, dip.X);
                Canvas.SetTop(label, dip.Y);

                // Write the rendered size back to the model (physical px) for bounds.
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                t.Width = label.DesiredSize.Width * _scale;
                t.Height = label.DesiredSize.Height * _scale;
                break;
        }
    }

    private void PlaceRectangle(Rectangle rect, PhysicalPoint start, PhysicalPoint end)
    {
        Point a = _toDip(start);
        Point b = _toDip(end);
        Canvas.SetLeft(rect, Math.Min(a.X, b.X));
        Canvas.SetTop(rect, Math.Min(a.Y, b.Y));
        rect.Width = Math.Abs(b.X - a.X);
        rect.Height = Math.Abs(b.Y - a.Y);
    }

    private System.Windows.Media.Effects.Effect? CreateShadow(bool enabled)
    {
        if (!enabled) return null;
        var effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Colors.Black,
            BlurRadius = 4 / _scale,
            ShadowDepth = 3 / _scale,
            Direction = 315,
            Opacity = 0.45,
        };
        effect.Freeze();
        return effect;
    }

    private Geometry BuildArrowGeometry(Point from, Point to, double thickness, ArrowStyle style)
    {
        Vector direction = to - from;
        double length = direction.Length;
        if (length < 0.001) return Geometry.Empty;
        direction.Normalize();
        Vector normal = new(-direction.Y, direction.X);
        double head = Math.Min(length * 0.65, Math.Max(8 / _scale, thickness * 3.5));
        Point neck = to - direction * head;
        double wing = head * 0.48;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            if (style == ArrowStyle.Open)
            {
                context.BeginFigure(from, false, false);
                context.LineTo(to, true, false);
                context.BeginFigure(neck + normal * wing, false, false);
                context.LineTo(to, true, false);
                context.LineTo(neck - normal * wing, true, false);
            }
            else
            {
                double half = Math.Min(thickness / 2, wing * 0.5);
                double tail = style == ArrowStyle.Tapered ? 0 : half;
                context.BeginFigure(from + normal * tail, true, true);
                context.LineTo(neck + normal * half, true, false);
                context.LineTo(neck + normal * wing, true, false);
                context.LineTo(to, true, false);
                context.LineTo(neck - normal * wing, true, false);
                context.LineTo(neck - normal * half, true, false);
                context.LineTo(from - normal * tail, true, false);
            }
        }
        geometry.Freeze();
        return geometry;
    }

    private static SolidColorBrush Brush(ArgbColor c) => new(Color.FromArgb(c.A, c.R, c.G, c.B));
}
