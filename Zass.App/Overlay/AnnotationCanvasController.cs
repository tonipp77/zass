using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Zass.Core.Annotations;
using Zass.Core.Commands;
using Zass.Interop;

namespace Zass.App.Overlay;

/// <summary>
/// Presentation-layer bridge between the pure annotation model (<see cref="Zass.Core"/>)
/// and the WPF <see cref="Canvas"/>. It materializes each model annotation as a
/// <see cref="Shape"/>, drives the live drawing preview, and routes undo/redo
/// through the <see cref="UndoRedoManager"/>. The model stays UI-free; this is the
/// single place that knows about WPF shapes (see Arquitectura §6.1).
/// </summary>
internal sealed class AnnotationCanvasController
{
    /// <summary>Below this side length (physical px) a drag is treated as an accidental click.</summary>
    private const double MinAnnotationSizePhysical = 3.0;

    private readonly Canvas _canvas;
    private readonly AnnotationLayer _layer = new();
    private readonly UndoRedoManager _history = new();
    private readonly Func<PhysicalPoint, Point> _toDip;
    private readonly double _scale;
    private readonly Dictionary<Annotation, Shape> _shapes = new();
    private readonly Rectangle _preview;

    public AnnotationCanvasController(Canvas canvas, Func<PhysicalPoint, Point> toDip, double scale)
    {
        _canvas = canvas;
        _toDip = toDip;
        _scale = scale;

        _preview = new Rectangle { Visibility = Visibility.Collapsed, IsHitTestVisible = false };
        Panel.SetZIndex(_preview, int.MaxValue);
        _canvas.Children.Add(_preview);
    }

    public bool CanUndo => _history.CanUndo;

    public bool CanRedo => _history.CanRedo;

    public bool HasAnnotations => _layer.Items.Count > 0;

    /// <summary>Shows/updates the rubber-band rectangle while the user drags.</summary>
    public void ShowRectanglePreview(PhysicalRect rect, ArgbColor color, double thicknessPhysical)
    {
        Point tl = _toDip(new PhysicalPoint(rect.X, rect.Y));
        Point br = _toDip(new PhysicalPoint(rect.Right, rect.Bottom));

        Canvas.SetLeft(_preview, tl.X);
        Canvas.SetTop(_preview, tl.Y);
        _preview.Width = Math.Max(0, br.X - tl.X);
        _preview.Height = Math.Max(0, br.Y - tl.Y);
        _preview.Stroke = Brush(color);
        _preview.StrokeThickness = thicknessPhysical / _scale;
        _preview.Visibility = Visibility.Visible;
    }

    public void HidePreview() => _preview.Visibility = Visibility.Collapsed;

    /// <summary>
    /// Commits a drawn rectangle as a model annotation via an undoable command.
    /// Returns false (and adds nothing) when the drag was too small to be intentional.
    /// </summary>
    public bool CommitRectangle(PhysicalPoint start, PhysicalPoint end, ArgbColor color, double thicknessPhysical)
    {
        HidePreview();

        if (Math.Abs(end.X - start.X) < MinAnnotationSizePhysical ||
            Math.Abs(end.Y - start.Y) < MinAnnotationSizePhysical)
        {
            return false;
        }

        var annotation = new RectangleAnnotation(start, end)
        {
            Color = color,
            Thickness = thicknessPhysical,
        };
        _history.Execute(new AddAnnotationCommand(_layer, annotation));
        Resync();
        return true;
    }

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

    /// <summary>Reconciles the canvas shapes with the current model state and z-order.</summary>
    private void Resync()
    {
        var live = new HashSet<Annotation>(_layer.Items);
        foreach (KeyValuePair<Annotation, Shape> entry in _shapes.Where(e => !live.Contains(e.Key)).ToList())
        {
            _canvas.Children.Remove(entry.Value);
            _shapes.Remove(entry.Key);
        }

        for (int i = 0; i < _layer.Items.Count; i++)
        {
            Annotation annotation = _layer.Items[i];
            if (!_shapes.TryGetValue(annotation, out Shape? shape))
            {
                shape = CreateShape(annotation);
                _shapes[annotation] = shape;
                _canvas.Children.Add(shape);
            }

            UpdateShape(shape, annotation);
            Panel.SetZIndex(shape, i);
        }
    }

    private static Shape CreateShape(Annotation annotation) => annotation switch
    {
        // Annotations don't intercept the mouse in this increment; the pointer tool
        // still drives the selection. Per-annotation hit-testing arrives later.
        RectangleAnnotation => new Rectangle { IsHitTestVisible = false },
        _ => throw new NotSupportedException($"No renderer for {annotation.GetType().Name} yet."),
    };

    private void UpdateShape(Shape shape, Annotation annotation)
    {
        switch (annotation)
        {
            case RectangleAnnotation r:
                var rect = (Rectangle)shape;
                Point p1 = _toDip(r.Start);
                Point p2 = _toDip(r.End);
                Canvas.SetLeft(rect, Math.Min(p1.X, p2.X));
                Canvas.SetTop(rect, Math.Min(p1.Y, p2.Y));
                rect.Width = Math.Abs(p2.X - p1.X);
                rect.Height = Math.Abs(p2.Y - p1.Y);
                rect.Stroke = Brush(r.Color);
                rect.StrokeThickness = r.Thickness / _scale;
                rect.Fill = null;
                break;
        }
    }

    private static SolidColorBrush Brush(ArgbColor c) => new(Color.FromArgb(c.A, c.R, c.G, c.B));
}
