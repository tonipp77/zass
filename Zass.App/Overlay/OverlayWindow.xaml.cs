using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using Zass.App.Imaging;
using Zass.App.Resources;
using Zass.Core.Annotations;
using Zass.Core.Capture;
using Zass.Interop;

namespace Zass.App.Overlay;

/// <summary>
/// Full-screen frozen overlay over the active monitor. The user drags a selection,
/// refines it with eight handles, draws vector annotations with the floating
/// toolbar, and confirms with Enter to copy the cropped region to the clipboard.
/// All selection/annotation state is kept in physical pixels; DIP is used only for drawing.
/// </summary>
public partial class OverlayWindow : Window
{
    /// <summary>Side length of a resize handle, in DIP.</summary>
    private const double HandleSizeDip = 8.0;

    /// <summary>Click slack around a handle center, in DIP.</summary>
    private const double HandleHitRadiusDip = 7.0;

    /// <summary>Gap between the selection and the floating toolbar, in DIP.</summary>
    private const double ToolbarGapDip = 8.0;

    private enum DragMode
    {
        None,
        DrawingSelection,
        MovingSelection,
        ResizingSelection,
        DrawingAnnotation,
    }

    private readonly CapturedImage _capture;
    private readonly double _scaleX;
    private readonly double _scaleY;
    private readonly Dictionary<SelectionHandle, Rectangle> _handles = new();
    private readonly AnnotationCanvasController _annotations;

    private OverlayTool _tool = OverlayTool.Pointer;

    // Default annotation style; a color/thickness picker arrives in Increment 5.
    private ArgbColor _currentColor = ArgbColor.Red;
    private double _currentThickness = 3.0;

    private DragMode _mode;
    private SelectionHandle _activeHandle;
    private (int X, int Y) _dragStart;
    private (int X, int Y) _annoStart;
    private (int X, int Y) _lastPhysical;
    private PhysicalRect _selection;
    private bool _hasSelection;

    public OverlayWindow(CapturedImage capture)
    {
        _capture = capture;
        _scaleX = capture.ScaleX;
        _scaleY = capture.ScaleY;

        InitializeComponent();

        BackgroundImage.Source = BitmapConvert.CreateBackground(capture);
        CreateHandles();

        _annotations = new AnnotationCanvasController(AnnotationCanvas, PhysicalPointToDip, _scaleX);

        PointerToolButton.ToolTip = Strings.ToolPointer;
        RectangleToolButton.ToolTip = Strings.ToolRectangle;
        PointerToolButton.IsChecked = true;

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        SizeChanged += (_, _) => UpdateVisuals();
    }

    private int HandleHitRadiusPhysical => (int)Math.Round(HandleHitRadiusDip * _scaleX);

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        // Place the window in physical pixels so it covers the monitor exactly.
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        WindowPlacement.CoverPhysicalRect(hwnd, _capture.PhysicalBounds);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Activate();
        Focus();
        Keyboard.Focus(this);
        UpdateVisuals();
    }

    // --- Coordinate conversion (the single DIP <-> physical boundary) ---

    private (int X, int Y) DipToPhysical(Point dip) => (
        _capture.PhysicalBounds.X + (int)Math.Round(dip.X * _scaleX),
        _capture.PhysicalBounds.Y + (int)Math.Round(dip.Y * _scaleY));

    private Rect PhysicalToDip(PhysicalRect r) => new(
        (r.X - _capture.PhysicalBounds.X) / _scaleX,
        (r.Y - _capture.PhysicalBounds.Y) / _scaleY,
        r.Width / _scaleX,
        r.Height / _scaleY);

    private Point PhysicalPointToDip(PhysicalPoint p) => new(
        (p.X - _capture.PhysicalBounds.X) / _scaleX,
        (p.Y - _capture.PhysicalBounds.Y) / _scaleY);

    private (int X, int Y) ClampToBounds(int px, int py)
    {
        PhysicalRect b = _capture.PhysicalBounds;
        return (Math.Clamp(px, b.X, b.Right), Math.Clamp(py, b.Y, b.Bottom));
    }

    // --- Tool selection ---

    private void OnPointerToolClick(object sender, RoutedEventArgs e) => SetTool(OverlayTool.Pointer);

    private void OnRectangleToolClick(object sender, RoutedEventArgs e) => SetTool(OverlayTool.Rectangle);

    private void SetTool(OverlayTool tool)
    {
        _tool = tool;
        PointerToolButton.IsChecked = tool == OverlayTool.Pointer;
        RectangleToolButton.IsChecked = tool == OverlayTool.Rectangle;
        Cursor = tool == OverlayTool.Rectangle ? Cursors.Cross : Cursors.Arrow;

        // Keep keyboard focus on the window so Enter/Ctrl+C/Esc keep working after a
        // toolbar click (the buttons are non-focusable, this is belt-and-braces).
        Keyboard.Focus(this);
    }

    private void OnToolbarMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Clicking the toolbar chrome must not start a drag on the canvas behind it.
        e.Handled = true;
    }

    // --- Mouse selection / manipulation / drawing ---

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        (int px, int py) = DipToPhysical(e.GetPosition(this));

        // Until there is a selection, any drag rubber-bands a new selection.
        if (!_hasSelection)
        {
            BeginSelectionDraw(px, py);
            return;
        }

        if (_tool == OverlayTool.Rectangle)
        {
            _mode = DragMode.DrawingAnnotation;
            _annoStart = ClampToBounds(px, py);
            CaptureMouse();
            return;
        }

        // Pointer tool: grab a handle, move the selection, or start a new one.
        SelectionHandle handle = SelectionManipulator.HitTest(_selection, px, py, HandleHitRadiusPhysical);
        if (handle == SelectionHandle.Inside)
        {
            _mode = DragMode.MovingSelection;
            _lastPhysical = (px, py);
            CaptureMouse();
            return;
        }

        if (handle != SelectionHandle.None)
        {
            _mode = DragMode.ResizingSelection;
            _activeHandle = handle;
            CaptureMouse();
            return;
        }

        BeginSelectionDraw(px, py);
    }

    private void BeginSelectionDraw(int px, int py)
    {
        _mode = DragMode.DrawingSelection;
        _dragStart = (px, py);
        _selection = default;
        _hasSelection = false;
        CaptureMouse();
        UpdateVisuals();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        (int px, int py) = DipToPhysical(e.GetPosition(this));

        switch (_mode)
        {
            case DragMode.DrawingSelection:
                PhysicalRect raw = SelectionGeometry.Normalize(_dragStart.X, _dragStart.Y, px, py);
                _selection = SelectionGeometry.ClampToBounds(raw, _capture.PhysicalBounds);
                _hasSelection = !_selection.IsEmpty;
                UpdateVisuals();
                break;

            case DragMode.ResizingSelection:
                _selection = SelectionManipulator.Resize(
                    _selection, _activeHandle, px, py, _capture.PhysicalBounds, SelectionGeometry.MinSize);
                UpdateVisuals();
                break;

            case DragMode.MovingSelection:
                _selection = SelectionManipulator.Move(
                    _selection, px - _lastPhysical.X, py - _lastPhysical.Y, _capture.PhysicalBounds);
                _lastPhysical = (px, py);
                UpdateVisuals();
                break;

            case DragMode.DrawingAnnotation:
                (int cx, int cy) = ClampToBounds(px, py);
                PhysicalRect preview = SelectionGeometry.Normalize(_annoStart.X, _annoStart.Y, cx, cy);
                _annotations.ShowRectanglePreview(preview, _currentColor, _currentThickness);
                break;

            default:
                UpdateCursor(px, py);
                break;
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_mode == DragMode.None)
        {
            return;
        }

        DragMode ended = _mode;
        _mode = DragMode.None;
        ReleaseMouseCapture();

        switch (ended)
        {
            case DragMode.DrawingSelection
                when !(_hasSelection && SelectionGeometry.IsValidSize(_selection)):
                // Accidental click: drop the tiny selection and keep waiting.
                _hasSelection = false;
                _selection = default;
                break;

            case DragMode.DrawingAnnotation:
                (int upX, int upY) = DipToPhysical(e.GetPosition(this));
                (int cx, int cy) = ClampToBounds(upX, upY);
                _annotations.CommitRectangle(
                    new PhysicalPoint(_annoStart.X, _annoStart.Y),
                    new PhysicalPoint(cx, cy),
                    _currentColor,
                    _currentThickness);
                break;
        }

        UpdateVisuals();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        if (ctrl)
        {
            switch (e.Key)
            {
                case Key.Z:
                    _annotations.Undo();
                    e.Handled = true;
                    return;
                case Key.Y:
                    _annotations.Redo();
                    e.Handled = true;
                    return;
                case Key.C:
                    if (_hasSelection && SelectionGeometry.IsValidSize(_selection))
                    {
                        ConfirmAndCopy();
                    }

                    e.Handled = true;
                    return;
            }

            return;
        }

        switch (e.Key)
        {
            case Key.Escape:
                Close();
                break;
            case Key.Enter:
                if (_hasSelection && SelectionGeometry.IsValidSize(_selection))
                {
                    ConfirmAndCopy();
                }

                break;
            case Key.V:
                SetTool(OverlayTool.Pointer);
                break;
            case Key.R:
                SetTool(OverlayTool.Rectangle);
                break;
        }
    }

    // --- Visuals ---

    private void CreateHandles()
    {
        foreach (SelectionHandle handle in new[]
                 {
                     SelectionHandle.TopLeft, SelectionHandle.Top, SelectionHandle.TopRight,
                     SelectionHandle.Right, SelectionHandle.BottomRight, SelectionHandle.Bottom,
                     SelectionHandle.BottomLeft, SelectionHandle.Left,
                 })
        {
            var rect = new Rectangle
            {
                Width = HandleSizeDip,
                Height = HandleSizeDip,
                Fill = Brushes.White,
                Stroke = new SolidColorBrush(Color.FromRgb(0x3C, 0x8D, 0xE5)),
                StrokeThickness = 1,
                Visibility = Visibility.Collapsed,
            };
            _handles[handle] = rect;
            ChromeCanvas.Children.Add(rect);
        }
    }

    private void UpdateVisuals()
    {
        var full = new Rect(0, 0, ActualWidth, ActualHeight);

        if (_hasSelection && !_selection.IsEmpty)
        {
            Rect sel = PhysicalToDip(_selection);

            var dim = new GeometryGroup { FillRule = FillRule.EvenOdd };
            dim.Children.Add(new RectangleGeometry(full));
            dim.Children.Add(new RectangleGeometry(sel));
            DimPath.Data = dim;

            Canvas.SetLeft(SelectionBorder, sel.X);
            Canvas.SetTop(SelectionBorder, sel.Y);
            SelectionBorder.Width = sel.Width;
            SelectionBorder.Height = sel.Height;
            SelectionBorder.Visibility = Visibility.Visible;

            DimensionText.Text = $"{_selection.Width} × {_selection.Height}";
            double labelTop = sel.Y > 24 ? sel.Y - 22 : sel.Y + 4;
            Canvas.SetLeft(DimensionLabel, sel.X);
            Canvas.SetTop(DimensionLabel, labelTop);
            DimensionLabel.Visibility = Visibility.Visible;

            // Handles are hidden while the user is still drawing the first box.
            UpdateHandles(sel, show: _mode != DragMode.DrawingSelection);
            UpdateToolbar(sel, show: _mode != DragMode.DrawingSelection);
        }
        else
        {
            DimPath.Data = new RectangleGeometry(full);
            SelectionBorder.Visibility = Visibility.Collapsed;
            DimensionLabel.Visibility = Visibility.Collapsed;
            UpdateHandles(default, show: false);
            UpdateToolbar(default, show: false);
        }
    }

    private void UpdateHandles(Rect sel, bool show)
    {
        if (!show)
        {
            foreach (Rectangle rect in _handles.Values)
            {
                rect.Visibility = Visibility.Collapsed;
            }

            return;
        }

        double offset = HandleSizeDip / 2.0;
        foreach ((SelectionHandle handle, Rectangle rect) in _handles)
        {
            Point center = HandleCenterDip(sel, handle);
            Canvas.SetLeft(rect, center.X - offset);
            Canvas.SetTop(rect, center.Y - offset);
            rect.Visibility = Visibility.Visible;
        }
    }

    private void UpdateToolbar(Rect sel, bool show)
    {
        if (!show)
        {
            Toolbar.Visibility = Visibility.Collapsed;
            return;
        }

        Toolbar.Visibility = Visibility.Visible;
        Toolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double width = Toolbar.DesiredSize.Width;
        double height = Toolbar.DesiredSize.Height;

        // Prefer below the selection; flip above when there is no room.
        double top = sel.Bottom + ToolbarGapDip;
        if (top + height > ActualHeight)
        {
            top = sel.Y - ToolbarGapDip - height;
        }

        top = Math.Clamp(top, 0, Math.Max(0, ActualHeight - height));
        double left = Math.Clamp(sel.X, 0, Math.Max(0, ActualWidth - width));

        Canvas.SetLeft(Toolbar, left);
        Canvas.SetTop(Toolbar, top);
    }

    private static Point HandleCenterDip(Rect r, SelectionHandle handle)
    {
        double midX = r.X + r.Width / 2;
        double midY = r.Y + r.Height / 2;
        return handle switch
        {
            SelectionHandle.TopLeft => new Point(r.Left, r.Top),
            SelectionHandle.Top => new Point(midX, r.Top),
            SelectionHandle.TopRight => new Point(r.Right, r.Top),
            SelectionHandle.Right => new Point(r.Right, midY),
            SelectionHandle.BottomRight => new Point(r.Right, r.Bottom),
            SelectionHandle.Bottom => new Point(midX, r.Bottom),
            SelectionHandle.BottomLeft => new Point(r.Left, r.Bottom),
            SelectionHandle.Left => new Point(r.Left, midY),
            _ => new Point(midX, midY),
        };
    }

    private void UpdateCursor(int px, int py)
    {
        if (_tool == OverlayTool.Rectangle)
        {
            Cursor = Cursors.Cross;
            return;
        }

        if (!_hasSelection)
        {
            Cursor = Cursors.Cross;
            return;
        }

        SelectionHandle handle = SelectionManipulator.HitTest(_selection, px, py, HandleHitRadiusPhysical);
        Cursor = handle switch
        {
            SelectionHandle.TopLeft or SelectionHandle.BottomRight => Cursors.SizeNWSE,
            SelectionHandle.TopRight or SelectionHandle.BottomLeft => Cursors.SizeNESW,
            SelectionHandle.Top or SelectionHandle.Bottom => Cursors.SizeNS,
            SelectionHandle.Left or SelectionHandle.Right => Cursors.SizeWE,
            SelectionHandle.Inside => Cursors.SizeAll,
            _ => Cursors.Arrow,
        };
    }

    // --- Export ---

    private void ConfirmAndCopy()
    {
        var bmp = ComposeForExport();
        CopyToClipboardWithRetry(bmp);
        Close();
    }

    /// <summary>
    /// Composes the exported image: the cropped frozen background plus the vector
    /// annotations that fall inside the selection. Auxiliary layers (dimming,
    /// handles, border, toolbar) live on other canvases and are excluded by design.
    /// Full pixel-fidelity verification and disk save come in Increment 6.
    /// </summary>
    private System.Windows.Media.Imaging.BitmapSource ComposeForExport()
    {
        System.Windows.Media.Imaging.BitmapSource background =
            BitmapConvert.CropForExport(_capture, _selection);
        if (!_annotations.HasAnnotations)
        {
            return background;
        }

        int pw = _selection.Width;
        int ph = _selection.Height;

        int canvasPxW = Math.Max(1, (int)Math.Round(AnnotationCanvas.ActualWidth * _scaleX));
        int canvasPxH = Math.Max(1, (int)Math.Round(AnnotationCanvas.ActualHeight * _scaleY));

        var canvasBmp = new System.Windows.Media.Imaging.RenderTargetBitmap(
            canvasPxW, canvasPxH, 96 * _scaleX, 96 * _scaleY, PixelFormats.Pbgra32);
        canvasBmp.Render(AnnotationCanvas);

        int offX = _selection.X - _capture.PhysicalBounds.X;
        int offY = _selection.Y - _capture.PhysicalBounds.Y;
        int cropW = Math.Min(pw, canvasPxW - offX);
        int cropH = Math.Min(ph, canvasPxH - offY);
        if (offX < 0 || offY < 0 || cropW <= 0 || cropH <= 0)
        {
            return background;
        }

        var annotationsCrop = new System.Windows.Media.Imaging.CroppedBitmap(
            canvasBmp, new Int32Rect(offX, offY, cropW, cropH));

        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.DrawImage(background, new Rect(0, 0, pw, ph));
            dc.DrawImage(annotationsCrop, new Rect(0, 0, cropW, cropH));
        }

        var result = new System.Windows.Media.Imaging.RenderTargetBitmap(
            pw, ph, 96, 96, PixelFormats.Pbgra32);
        result.Render(visual);
        result.Freeze();
        return result;
    }

    private static void CopyToClipboardWithRetry(System.Windows.Media.Imaging.BitmapSource image)
    {
        const int maxAttempts = 5;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                Clipboard.SetImage(image);
                return;
            }
            catch (COMException) when (attempt < maxAttempts)
            {
                // Clipboard is briefly locked by another app; back off and retry.
                Thread.Sleep(50);
            }
        }
    }
}
