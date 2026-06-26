using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
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

    // In-progress drag annotation (rectangle/filled/arrow/freehand) not yet committed.
    private Annotation? _draft;

    // In-progress inline text editor; non-null only while the user is typing.
    private TextBox? _editingTextBox;
    private PhysicalPoint _editPosition;
    private ArgbColor _editColor;
    private double _editFontSize;

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
    public void BeginDraft(OverlayTool tool, PhysicalPoint start, ArgbColor color, double thicknessPhysical)
    {
        Annotation annotation = tool switch
        {
            OverlayTool.Rectangle => new RectangleAnnotation(start, start) { Thickness = thicknessPhysical },
            OverlayTool.FilledRectangle => new FilledRectangleAnnotation(start, start),
            OverlayTool.Arrow => new ArrowAnnotation(start, start) { Thickness = thicknessPhysical },
            OverlayTool.Freehand => new FreehandAnnotation(new[] { start }) { Thickness = thicknessPhysical },
            _ => throw new NotSupportedException($"{tool} is not a drag-drawn tool."),
        };
        annotation.Color = color;

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

        if (!IsDraftMeaningful(annotation))
        {
            if (_visuals.Remove(annotation, out FrameworkElement? visual))
            {
                _canvas.Children.Remove(visual);
            }

            return false;
        }

        // The visual already exists and is registered; Execute adds the model and
        // Resync just restores the proper z-order.
        _history.Execute(new AddAnnotationCommand(_layer, annotation));
        Resync();
        return true;
    }

    private static bool IsDraftMeaningful(Annotation annotation) => annotation switch
    {
        RectangleAnnotation r => HasMinSpan(r.Start, r.End),
        FilledRectangleAnnotation fr => HasMinSpan(fr.Start, fr.End),
        ArrowAnnotation ar => ar.From.DistanceTo(ar.To) >= MinAnnotationSizePhysical,
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
    public void BeginText(PhysicalPoint position, ArgbColor color, double fontSizePhysical)
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
            var annotation = new TextAnnotation(_editPosition, box.Text, _editFontSize) { Color = _editColor };
            _history.Execute(new AddAnnotationCommand(_layer, annotation));
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
        switch (annotation)
        {
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
                path.Data = BuildArrowGeometry(_toDip(ar.From), _toDip(ar.To), arrowThicknessDip);
                path.Stroke = Brush(ar.Color);
                path.StrokeThickness = arrowThicknessDip;
                path.StrokeStartLineCap = PenLineCap.Round;
                path.StrokeEndLineCap = PenLineCap.Round;
                path.StrokeLineJoin = PenLineJoin.Round;
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

    /// <summary>Builds an open (two-barb) arrowhead plus shaft, in DIP space.</summary>
    private static Geometry BuildArrowGeometry(Point from, Point to, double thicknessDip)
    {
        double dx = to.X - from.X;
        double dy = to.Y - from.Y;
        double length = Math.Sqrt((dx * dx) + (dy * dy));

        var geometry = new StreamGeometry();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            ctx.BeginFigure(from, false, false);
            ctx.LineTo(to, true, true);

            if (length > 0.001)
            {
                double ux = dx / length;
                double uy = dy / length;
                double head = Math.Min(length, Math.Max(8.0, thicknessDip * 3.5));
                const double spread = 0.5; // ~28.6° each side
                double cos = Math.Cos(spread);
                double sin = Math.Sin(spread);

                var barb1 = new Point(
                    to.X - (head * ((ux * cos) - (uy * sin))),
                    to.Y - (head * ((uy * cos) + (ux * sin))));
                var barb2 = new Point(
                    to.X - (head * ((ux * cos) + (uy * sin))),
                    to.Y - (head * ((uy * cos) - (ux * sin))));

                ctx.BeginFigure(barb1, false, false);
                ctx.LineTo(to, true, true);
                ctx.LineTo(barb2, true, true);
            }
        }

        geometry.Freeze();
        return geometry;
    }

    private static SolidColorBrush Brush(ArgbColor c) => new(Color.FromArgb(c.A, c.R, c.G, c.B));
}
