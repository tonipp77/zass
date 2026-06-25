using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using Zass.App.Imaging;
using Zass.Core.Capture;
using Zass.Interop;

namespace Zass.App.Overlay;

/// <summary>
/// Full-screen frozen overlay over the active monitor. The user drags a
/// rectangular selection, then refines it with eight resize handles or by moving
/// it, and confirms with Enter to copy the cropped region to the clipboard.
/// All selection state is kept in physical pixels; DIP is used only for drawing.
/// </summary>
public partial class OverlayWindow : Window
{
    /// <summary>Side length of a resize handle, in DIP.</summary>
    private const double HandleSizeDip = 8.0;

    /// <summary>Click slack around a handle center, in DIP.</summary>
    private const double HandleHitRadiusDip = 7.0;

    private enum DragMode
    {
        None,
        Drawing,
        Moving,
        Resizing,
    }

    private readonly CapturedImage _capture;
    private readonly double _scaleX;
    private readonly double _scaleY;
    private readonly Dictionary<SelectionHandle, Rectangle> _handles = new();

    private DragMode _mode;
    private SelectionHandle _activeHandle;
    private (int X, int Y) _dragStart;
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

    // --- Mouse selection / manipulation ---

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        (int px, int py) = DipToPhysical(e.GetPosition(this));

        if (_hasSelection)
        {
            SelectionHandle handle = SelectionManipulator.HitTest(_selection, px, py, HandleHitRadiusPhysical);
            if (handle == SelectionHandle.Inside)
            {
                _mode = DragMode.Moving;
                _lastPhysical = (px, py);
                CaptureMouse();
                return;
            }

            if (handle != SelectionHandle.None)
            {
                _mode = DragMode.Resizing;
                _activeHandle = handle;
                CaptureMouse();
                return;
            }
        }

        // Empty space (or no selection yet): start a fresh rubber-band selection.
        _mode = DragMode.Drawing;
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
            case DragMode.Drawing:
                PhysicalRect raw = SelectionGeometry.Normalize(_dragStart.X, _dragStart.Y, px, py);
                _selection = SelectionGeometry.ClampToBounds(raw, _capture.PhysicalBounds);
                _hasSelection = !_selection.IsEmpty;
                UpdateVisuals();
                break;

            case DragMode.Resizing:
                _selection = SelectionManipulator.Resize(
                    _selection, _activeHandle, px, py, _capture.PhysicalBounds, SelectionGeometry.MinSize);
                UpdateVisuals();
                break;

            case DragMode.Moving:
                _selection = SelectionManipulator.Move(
                    _selection, px - _lastPhysical.X, py - _lastPhysical.Y, _capture.PhysicalBounds);
                _lastPhysical = (px, py);
                UpdateVisuals();
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

        if (ended == DragMode.Drawing && !(_hasSelection && SelectionGeometry.IsValidSize(_selection)))
        {
            // Accidental click: drop the tiny selection and keep waiting.
            _hasSelection = false;
            _selection = default;
        }

        UpdateVisuals();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
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
            OverlayCanvas.Children.Add(rect);
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
            UpdateHandles(sel, show: _mode != DragMode.Drawing);
        }
        else
        {
            DimPath.Data = new RectangleGeometry(full);
            SelectionBorder.Visibility = Visibility.Collapsed;
            DimensionLabel.Visibility = Visibility.Collapsed;
            UpdateHandles(default, show: false);
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
            _ => Cursors.Cross,
        };
    }

    // --- Export ---

    private void ConfirmAndCopy()
    {
        var bmp = BitmapConvert.CropForExport(_capture, _selection);
        CopyToClipboardWithRetry(bmp);
        Close();
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
