using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Zass.App.Imaging;
using Zass.Core.Capture;
using Zass.Interop;

namespace Zass.App.Overlay;

/// <summary>
/// Full-screen frozen overlay over the active monitor. Lets the user drag a
/// rectangular selection and copies the cropped region to the clipboard.
/// All selection state is kept in physical pixels; DIP is used only for drawing.
/// </summary>
public partial class OverlayWindow : Window
{
    private readonly CapturedImage _capture;
    private readonly double _scaleX;
    private readonly double _scaleY;

    private bool _dragging;
    private (int X, int Y) _dragStart;
    private PhysicalRect _selection;
    private bool _hasSelection;

    public OverlayWindow(CapturedImage capture)
    {
        _capture = capture;
        _scaleX = capture.ScaleX;
        _scaleY = capture.ScaleY;

        InitializeComponent();

        BackgroundImage.Source = BitmapConvert.CreateBackground(capture);

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        SizeChanged += (_, _) => UpdateVisuals();
    }

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

    // --- Mouse selection ---

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        _dragging = true;
        _dragStart = DipToPhysical(e.GetPosition(this));
        _selection = default;
        _hasSelection = false;
        CaptureMouse();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging)
        {
            return;
        }

        (int x, int y) = DipToPhysical(e.GetPosition(this));
        PhysicalRect raw = SelectionGeometry.Normalize(_dragStart.X, _dragStart.Y, x, y);
        _selection = SelectionGeometry.ClampToBounds(raw, _capture.PhysicalBounds);
        _hasSelection = !_selection.IsEmpty;
        UpdateVisuals();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        ReleaseMouseCapture();

        if (_hasSelection && SelectionGeometry.IsValidSize(_selection))
        {
            ConfirmAndCopy();
        }
        else
        {
            // Treat as an accidental click: reset and keep waiting.
            _hasSelection = false;
            _selection = default;
            UpdateVisuals();
        }
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
        }
        else
        {
            DimPath.Data = new RectangleGeometry(full);
            SelectionBorder.Visibility = Visibility.Collapsed;
            DimensionLabel.Visibility = Visibility.Collapsed;
        }
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
