using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
using Zass.Core.Export;
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

    // Active annotation style; seeded from the persisted settings (RF-21) and updated
    // by the toolbar. The final values are read back on close to remember them.
    private ArgbColor _currentColor;
    private double _currentThickness;
    private double _currentTextSize;

    // Export defaults from settings: format pre-selected in the Save dialog and the
    // JPEG quality used when saving as JPG.
    private readonly ImageExportFormat _defaultFormat;
    private readonly int _jpegQuality;

    private DragMode _mode;
    private SelectionHandle _activeHandle;
    private (int X, int Y) _dragStart;
    private (int X, int Y) _lastPhysical;
    private PhysicalRect _selection;
    private bool _hasSelection;

    // Guards the color popup against the closing click immediately reopening it.
    private bool _suppressColorReopen;

    public OverlayWindow(CapturedImage capture, OverlayOptions options)
    {
        _capture = capture;
        _scaleX = capture.ScaleX;
        _scaleY = capture.ScaleY;

        _currentColor = options.InitialColor;
        _currentThickness = options.InitialThickness;
        _currentTextSize = options.InitialTextSize;
        _defaultFormat = options.DefaultFormat;
        _jpegQuality = options.JpegQuality;

        InitializeComponent();

        BackgroundImage.Source = BitmapConvert.CreateBackground(capture);
        CreateHandles();

        _annotations = new AnnotationCanvasController(AnnotationCanvas, PhysicalPointToDip, _scaleX);
        _annotations.TextEditCompleted += () => Keyboard.Focus(this);

        PointerToolButton.ToolTip = Strings.ToolPointer;
        TextToolButton.ToolTip = Strings.ToolText;
        ArrowToolButton.ToolTip = Strings.ToolArrow;
        RectangleToolButton.ToolTip = Strings.ToolRectangle;
        FilledRectangleToolButton.ToolTip = Strings.ToolFilledRectangle;
        FreehandToolButton.ToolTip = Strings.ToolFreehand;
        CopyButton.ToolTip = Strings.ToolCopy;
        SaveButton.ToolTip = Strings.ToolSave;
        CloseButton.ToolTip = Strings.ToolClose;
        HintText.Text = Strings.OverlayHint;
        PointerToolButton.IsChecked = true;

        InitializeToolOptions();

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        SizeChanged += (_, _) => UpdateVisuals();
    }

    /// <summary>The color in effect when the overlay closed, to persist for next time (RF-21).</summary>
    public ArgbColor LastColor => _currentColor;

    /// <summary>The stroke thickness in effect when the overlay closed (RF-21).</summary>
    public double LastThickness => _currentThickness;

    /// <summary>The text size in effect when the overlay closed (RF-21).</summary>
    public double LastTextSize => _currentTextSize;

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

    private void OnTextToolClick(object sender, RoutedEventArgs e) => SetTool(OverlayTool.Text);

    private void OnArrowToolClick(object sender, RoutedEventArgs e) => SetTool(OverlayTool.Arrow);

    private void OnRectangleToolClick(object sender, RoutedEventArgs e) => SetTool(OverlayTool.Rectangle);

    private void OnFilledRectangleToolClick(object sender, RoutedEventArgs e) => SetTool(OverlayTool.FilledRectangle);

    private void OnFreehandToolClick(object sender, RoutedEventArgs e) => SetTool(OverlayTool.Freehand);

    private static bool IsDrawingTool(OverlayTool tool) => tool is
        OverlayTool.Rectangle or OverlayTool.FilledRectangle or OverlayTool.Arrow or OverlayTool.Freehand;

    private void SetTool(OverlayTool tool)
    {
        _tool = tool;
        PointerToolButton.IsChecked = tool == OverlayTool.Pointer;
        TextToolButton.IsChecked = tool == OverlayTool.Text;
        ArrowToolButton.IsChecked = tool == OverlayTool.Arrow;
        RectangleToolButton.IsChecked = tool == OverlayTool.Rectangle;
        FilledRectangleToolButton.IsChecked = tool == OverlayTool.FilledRectangle;
        FreehandToolButton.IsChecked = tool == OverlayTool.Freehand;
        Cursor = tool == OverlayTool.Pointer ? Cursors.Arrow : Cursors.Cross;

        UpdateToolOptions();

        // The toolbar width changes with the visible options, so re-anchor it.
        if (_hasSelection)
        {
            UpdateVisuals();
        }

        // Keep keyboard focus on the window so Enter/Ctrl+C/Esc keep working after a
        // toolbar click (the buttons are non-focusable, this is belt-and-braces).
        Keyboard.Focus(this);
    }

    private void OnToolbarMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Clicking the toolbar chrome must not start a drag on the canvas behind it.
        e.Handled = true;
    }

    // --- Color / thickness / size options ---

    private void InitializeToolOptions()
    {
        ColorButton.ToolTip = Strings.ColorPicker;
        ThicknessLabel.Text = Strings.LabelThickness;
        TextSizeLabel.Text = Strings.LabelTextSize;

        ColorPickerControl.SelectedColor = ToMediaColor(_currentColor);
        ColorSwatch.Background = new SolidColorBrush(ToMediaColor(_currentColor));
        ColorPickerControl.SelectedColorChanged += OnPickerColorChanged;
        ColorPickerControl.ColorCommitted += (_, _) => ColorPopup.IsOpen = false;

        // Seed the sliders from the persisted style (RF-21). Setting a value that
        // differs from the XAML default fires ValueChanged and refreshes its caption;
        // seed the captions explicitly to also cover the value-equals-default case.
        ThicknessSlider.Value = _currentThickness;
        TextSizeSlider.Value = _currentTextSize;
        ThicknessValue.Text = FormatValue(_currentThickness);
        TextSizeValue.Text = FormatValue(_currentTextSize);

        UpdateToolOptions();
    }

    private void UpdateToolOptions()
    {
        bool usesColor = _tool != OverlayTool.Pointer;
        bool usesStroke = _tool is OverlayTool.Arrow or OverlayTool.Rectangle or OverlayTool.Freehand;
        bool isText = _tool == OverlayTool.Text;

        OptionsSeparator.Visibility = usesColor ? Visibility.Visible : Visibility.Collapsed;
        ColorButton.Visibility = usesColor ? Visibility.Visible : Visibility.Collapsed;
        ThicknessPanel.Visibility = usesStroke ? Visibility.Visible : Visibility.Collapsed;
        TextSizePanel.Visibility = isText ? Visibility.Visible : Visibility.Collapsed;

        if (!usesColor && ColorPopup.IsOpen)
        {
            ColorPopup.IsOpen = false;
        }
    }

    private void OnColorButtonClick(object sender, RoutedEventArgs e)
    {
        // When the popup is open, the click first closes it (StaysOpen=False); the
        // suppression flag stops this same click from immediately reopening it.
        if (!_suppressColorReopen)
        {
            ColorPopup.IsOpen = true;
        }
    }

    private void OnColorPopupClosed(object? sender, EventArgs e)
    {
        _suppressColorReopen = true;
        Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Input,
            new Action(() => _suppressColorReopen = false));
    }

    private void OnPickerColorChanged(object? sender, EventArgs e)
    {
        Color c = ColorPickerControl.SelectedColor;
        _currentColor = new ArgbColor(c.A, c.R, c.G, c.B);
        ColorSwatch.Background = new SolidColorBrush(c);
    }

    private void OnThicknessChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _currentThickness = e.NewValue;
        if (ThicknessValue is not null)
        {
            ThicknessValue.Text = FormatValue(e.NewValue);
        }
    }

    private void OnTextSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _currentTextSize = e.NewValue;
        if (TextSizeValue is not null)
        {
            TextSizeValue.Text = FormatValue(e.NewValue);
        }
    }

    private static string FormatValue(double value) =>
        ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture);

    private static Color ToMediaColor(ArgbColor c) => Color.FromArgb(c.A, c.R, c.G, c.B);

    // --- Mouse selection / manipulation / drawing ---

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        // If this same click just dismissed the open color popup, consume it so it
        // doesn't also start drawing an annotation. The flag is only set for the one
        // input cycle in which the popup closed.
        if (_suppressColorReopen)
        {
            return;
        }

        // A click anywhere while typing confirms the current text and consumes the click.
        if (_annotations.IsEditingText)
        {
            _annotations.CommitText();
            return;
        }

        (int px, int py) = DipToPhysical(e.GetPosition(this));

        // Until there is a selection, any drag rubber-bands a new selection.
        if (!_hasSelection)
        {
            BeginSelectionDraw(px, py);
            return;
        }

        if (_tool == OverlayTool.Text)
        {
            (int tx, int ty) = ClampToBounds(px, py);
            _annotations.BeginText(new PhysicalPoint(tx, ty), _currentColor, _currentTextSize);
            return;
        }

        if (IsDrawingTool(_tool))
        {
            _mode = DragMode.DrawingAnnotation;
            (int sx, int sy) = ClampToBounds(px, py);
            _annotations.BeginDraft(_tool, new PhysicalPoint(sx, sy), _currentColor, _currentThickness);
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

    /// <summary>
    /// Selects the whole active monitor without dragging (RF-5). The selection becomes
    /// editable (handles/toolbar) exactly as a dragged one, so the user can still annotate.
    /// </summary>
    private void SelectFullMonitor()
    {
        if (_annotations.IsEditingText)
        {
            _annotations.CommitText();
        }

        _mode = DragMode.None;
        _selection = _capture.PhysicalBounds;
        _hasSelection = true;
        UpdateVisuals();
        Keyboard.Focus(this);
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
                _annotations.UpdateDraft(new PhysicalPoint(cx, cy));
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
                _annotations.UpdateDraft(new PhysicalPoint(cx, cy));
                _annotations.CommitDraft();
                break;
        }

        UpdateVisuals();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // While the inline text editor has focus, let it own every keystroke: typing
        // must not trigger single-key tool shortcuts (RF-13). Enter/Esc are confirmed
        // by the editor itself (which marks them handled before they reach here).
        if (_annotations.IsEditingText)
        {
            return;
        }

        // A focused text field (e.g. the hex input in the color popup) owns its
        // keystrokes; single-key tool shortcuts must not fire while typing into it.
        if (Keyboard.FocusedElement is TextBox)
        {
            return;
        }

        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        if (ctrl)
        {
            switch (e.Key)
            {
                case Key.A:
                    SelectFullMonitor();
                    e.Handled = true;
                    return;
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
                case Key.S:
                    if (_hasSelection && SelectionGeometry.IsValidSize(_selection))
                    {
                        ConfirmAndSave();
                    }

                    e.Handled = true;
                    return;
            }

            return;
        }

        switch (e.Key)
        {
            case Key.Escape:
                // Esc dismisses an open color popup first; otherwise it cancels capture.
                if (ColorPopup.IsOpen)
                {
                    ColorPopup.IsOpen = false;
                }
                else
                {
                    Close();
                }

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
            case Key.T:
                SetTool(OverlayTool.Text);
                break;
            case Key.A:
                SetTool(OverlayTool.Arrow);
                break;
            case Key.R:
                SetTool(OverlayTool.Rectangle);
                break;
            case Key.F:
                SetTool(OverlayTool.FilledRectangle);
                break;
            case Key.D:
                SetTool(OverlayTool.Freehand);
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

        // The hint is only useful until a selection exists (RF-5).
        HintBar.Visibility = _hasSelection && !_selection.IsEmpty
            ? Visibility.Collapsed
            : Visibility.Visible;

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
        if (_tool != OverlayTool.Pointer)
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

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        if (_hasSelection && SelectionGeometry.IsValidSize(_selection))
        {
            ConfirmAndCopy();
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (_hasSelection && SelectionGeometry.IsValidSize(_selection))
        {
            ConfirmAndSave();
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void ConfirmAndCopy()
    {
        // Flush any text still being typed so it lands in the exported image.
        _annotations.CommitText();
        var bmp = ComposeForExport();
        CopyToClipboardWithRetry(bmp);
        Close();
    }

    /// <summary>
    /// Saves the composed image to disk (RF-15, RF-16, RF-17). Shows the system Save
    /// dialog with a time-stamped default name and PNG/JPG filters; closes the overlay
    /// only on a successful write. Cancelling or a write error keeps the overlay open.
    /// </summary>
    private void ConfirmAndSave()
    {
        // Flush any text still being typed so it lands in the exported image.
        _annotations.CommitText();

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = Strings.SaveDialogTitle,
            FileName = ExportNaming.DefaultFileName(DateTime.Now, _defaultFormat),
            DefaultExt = ExportNaming.ExtensionFor(_defaultFormat),
            AddExtension = true,
            OverwritePrompt = true,
            Filter = $"{Strings.SaveFilterPng}|*.png|{Strings.SaveFilterJpeg}|*.jpg;*.jpeg",
            // Pre-select the user's preferred default format (RF-16). FilterIndex is 1-based.
            FilterIndex = _defaultFormat == ImageExportFormat.Jpeg ? 2 : 1,
        };

        // The overlay is topmost; drop that while the modal dialog is up so it isn't
        // hidden behind the full-screen overlay, then restore it if the user cancels.
        bool wasTopmost = Topmost;
        Topmost = false;
        bool? confirmed = dialog.ShowDialog(this);
        Topmost = wasTopmost;

        if (confirmed != true)
        {
            return; // Cancelled: keep the overlay open for further editing.
        }

        try
        {
            System.Windows.Media.Imaging.BitmapSource image = ComposeForExport();
            ImageExporter.Save(
                image, dialog.FileName, ExportNaming.FormatFromExtension(dialog.FileName), _jpegQuality);
        }
        catch (Exception ex)
        {
            // A failed write (permissions, locked file, full disk) must surface, not
            // crash; keep the overlay open so the user can retry or pick another path.
            Trace.TraceError($"Zass: save failed: {ex}");
            MessageBox.Show(this, Strings.SaveErrorMessage, Strings.HotkeyConflictTitle,
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

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
