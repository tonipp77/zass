using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Zass.App.Imaging;
using Zass.App.Localization;
using Zass.App.Resources;
using Zass.Core.Collage;
using Zass.Core.Annotations;
using Zass.Core.Export;
using Zass.Interop;

namespace Zass.App.Views;

public partial class CollageWindow : Window
{
    private readonly CollageDocument _document = new();
    private readonly Dictionary<Guid, BitmapSource> _images = new();
    private readonly Dictionary<Guid, Image> _visuals = new();
    private readonly List<BitmapSource> _captures = new();
    private CollageDecorationKind _lastArrowTool = CollageDecorationKind.CurvedArrow;
    private readonly Func<(ImageExportFormat Format, int Quality)> _exportOptions;
    private Guid? _selected;
    private CollageItem? _dragItem;
    private Point _dragStart;
    private int _pendingX;
    private int _pendingY;
    private Rectangle? _selectionFrame;
    private bool _exporting;
    private bool _allowClose;
    private CollageDecorationKind? _tool;
    private ArgbColor _color = ArgbColor.Red;
    private PhysicalPoint? _arrowStart;
    private CollageItem? _arrowDraft;
    private Image? _draftVisual;
    private TextBox? _textEditor;
    private double _textSize;
    private bool _suppressColorReopen;
    private CollageDecoration? _textDefinition;

    public CollageWindow(Func<(ImageExportFormat Format, int Quality)> exportOptions)
    {
        _exportOptions = exportOptions;
        InitializeComponent();
        ColorPickerControl.SelectedColor = Colors.Red;
        ColorPickerControl.SelectedColorChanged += (_, _) =>
        {
            Color c = ColorPickerControl.SelectedColor;
            _color = new ArgbColor(c.A, c.R, c.G, c.B);
            ColorSwatch.Background = new SolidColorBrush(c);
        };
        ColorPickerControl.ColorCommitted += (_, _) => ColorPopup.IsOpen = false;
        FontChoice.ItemsSource = new[] { "Segoe UI", "Arial", "Calibri", "Consolas", "Georgia" };
        FontChoice.SelectedIndex = 0;
        RefreshLanguage();
        LocalizationManager.LanguageChanged += OnLanguageChanged;
        Closing += OnClosing;
        Closed += (_, _) =>
        {
            LocalizationManager.LanguageChanged -= OnLanguageChanged;
            Surface.Children.Clear();
            _visuals.Clear();
            _images.Clear();
            _captures.Clear();
            CaptureStrip.Children.Clear();
        };
        PreviewKeyDown += OnKeyDown;
        Refresh();
    }

    public event Action? CaptureRequested;
    public bool IsExporting => _exporting;

    public bool PrepareForCapture()
    {
        FinishDrag();
        return CommitText();
    }

    public void RetainCapture(BitmapSource image)
    {
        ValidateCaptureBudget(image);
        if (_captures.Contains(image)) return;
        if (image.CanFreeze) image.Freeze();
        _captures.Add(image);
        RefreshCaptureStrip();
    }

    private void ValidateCaptureBudget(BitmapSource image)
    {
        // Duplicated canvas objects share one immutable bitmap with the capture strip.
        long pixels = _captures.Sum(i => (long)i.PixelWidth * i.PixelHeight);
        if (!_captures.Contains(image)) pixels += (long)image.PixelWidth * image.PixelHeight;
        if (pixels > CollageDocument.MaxPixels) throw new ArgumentOutOfRangeException(nameof(image));
    }

    public void AddCrop(BitmapSource image)
    {
        ValidateCaptureBudget(image);
        Guid id = _document.Add(image.PixelWidth, image.PixelHeight);
        RetainCapture(image);
        _images.Add(id, image);
        _selected = id;
        Refresh();
        SetTool(null);
    }

    private void RefreshCaptureStrip()
    {
        CaptureStrip.Children.Clear();
        BufferEmpty.Visibility = _captures.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        for (int index = 0; index < _captures.Count; index++)
        {
            BitmapSource bitmap = _captures[index];
            var content = new StackPanel();
            content.Children.Add(new Image { Source = bitmap, Width = 112, Height = 62, Stretch = Stretch.Uniform });
            content.Children.Add(new TextBlock
            {
                Text = string.Format(CultureInfo.CurrentCulture, Strings.CollageCaptureDimensions,
                    index + 1, bitmap.PixelWidth, bitmap.PixelHeight),
                FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0),
            });
            var button = new Button
            {
                Content = content, ToolTip = Strings.CollageInsertCapture,
                Padding = new Thickness(6), Margin = new Thickness(0, 0, 8, 0),
            };
            button.Click += (_, _) =>
            {
                if (_exporting || !PrepareForCapture()) return;
                try { AddCrop(bitmap); }
                catch (ArgumentOutOfRangeException ex) { ReportError(ex, Strings.CollageAddError); }
            };
            CaptureStrip.Children.Add(button);
        }
    }

    public async Task HideForCaptureAsync()
    {
        // Hide without the DWM fade, otherwise BitBlt can capture a translucent remnant.
        WindowComposition.DisableTransitions(new System.Windows.Interop.WindowInteropHelper(this).EnsureHandle());
        ColorPopup.IsOpen = false;
        Hide();
        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        // Keep the UI responsive while waiting for the compositor's next presentation.
        await Task.Run(WindowComposition.Flush);
    }

    public void Reveal()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    public void CloseForShutdown()
    {
        _allowClose = true;
        Close();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        if (!PrepareForCapture()) return;
        Hide(); // The tray entry reopens this session, including its history.
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (!PrepareForCapture()) return;
        RefreshLanguage();
        Refresh();
    }

    private void OnArrowTool(object sender, RoutedEventArgs e) => SetTool(_lastArrowTool);

    private void OnPointerTool(object sender, RoutedEventArgs e) => SetTool(null);
    private void OnCurvedTool(object sender, RoutedEventArgs e) => SetTool(CollageDecorationKind.CurvedArrow);
    private void OnStraightTool(object sender, RoutedEventArgs e) => SetTool(CollageDecorationKind.StraightArrow);
    private void OnElbowTool(object sender, RoutedEventArgs e) => SetTool(CollageDecorationKind.ElbowArrow);
    private void OnTextTool(object sender, RoutedEventArgs e) => SetTool(CollageDecorationKind.Text);
    private void OnStampTool(object sender, RoutedEventArgs e) => SetTool(CollageDecorationKind.Stamp);
    private void OnStepTool(object sender, RoutedEventArgs e) => SetTool(CollageDecorationKind.Step);
    private void OnLabelTool(object sender, RoutedEventArgs e) => SetTool(CollageDecorationKind.Label);

    private static bool IsTextTool(CollageDecorationKind? tool) =>
        tool is CollageDecorationKind.Text or CollageDecorationKind.Step or CollageDecorationKind.Label;
    private static bool IsArrowTool(CollageDecorationKind? tool) =>
        tool is CollageDecorationKind.CurvedArrow or CollageDecorationKind.StraightArrow or CollageDecorationKind.ElbowArrow;

    private CollageDecoration Definition(PhysicalPoint from, PhysicalPoint to) => new(
        _tool!.Value, from, to, _color, SizeSlider.Value,
        HasShadow: ShadowCheck.IsChecked == true, Opacity: OpacitySlider.Value / 100,
        FontFamily: FontChoice.SelectedItem as string ?? "Segoe UI",
        Bold: BoldCheck.IsChecked == true, Italic: ItalicCheck.IsChecked == true,
        TextOutline: OutlineCheck.IsChecked == true,
        Stamp: (CollageStamp)Math.Max(0, StampChoice.SelectedIndex),
        BadgeShape: (CollageBadgeShape)Math.Max(0, StepChoice.SelectedIndex),
        LabelShape: (CollageLabelShape)Math.Max(0, LabelChoice.SelectedIndex),
        ArrowTip: (CollageArrowTip)Math.Max(0, TipChoice.SelectedIndex), Dashed: DashedCheck.IsChecked == true);

    private void SetTool(CollageDecorationKind? tool)
    {
        if (!PrepareForCapture()) return;
        _tool = tool;
        PointerTool.IsChecked = tool is null;
        CurvedTool.IsChecked = tool == CollageDecorationKind.CurvedArrow;
        StraightTool.IsChecked = tool == CollageDecorationKind.StraightArrow;
        ElbowTool.IsChecked = tool == CollageDecorationKind.ElbowArrow;
        TextTool.IsChecked = tool == CollageDecorationKind.Text;
        StampTool.IsChecked = tool == CollageDecorationKind.Stamp;
        StepTool.IsChecked = tool == CollageDecorationKind.Step;
        LabelTool.IsChecked = tool == CollageDecorationKind.Label;
        StampChoice.Visibility = tool == CollageDecorationKind.Stamp ? Visibility.Visible : Visibility.Collapsed;
        StepChoice.Visibility = tool == CollageDecorationKind.Step ? Visibility.Visible : Visibility.Collapsed;
        LabelChoice.Visibility = tool == CollageDecorationKind.Label ? Visibility.Visible : Visibility.Collapsed;
        TextOptions.Visibility = IsTextTool(tool) ? Visibility.Visible : Visibility.Collapsed;
        OutlineCheck.Visibility = tool == CollageDecorationKind.Text ? Visibility.Visible : Visibility.Collapsed;
        bool isArrow = IsArrowTool(tool);
        ArrowOptions.Visibility = isArrow ? Visibility.Visible : Visibility.Collapsed;
        if (isArrow) _lastArrowTool = tool!.Value;
        ArrowTool.IsChecked = isArrow;
        ArrowStyles.Visibility = isArrow ? Visibility.Visible : Visibility.Collapsed;
        ToolProperties.Visibility = tool is null ? Visibility.Collapsed : Visibility.Visible;
        PointerHint.Visibility = tool is null ? Visibility.Visible : Visibility.Collapsed;
        if (tool is null) ColorPopup.IsOpen = false;
        SizeLabel.Text = IsTextTool(tool) ? Strings.LabelTextSize : Strings.CollageObjectSize;
        Surface.Cursor = tool is null ? Cursors.Arrow : IsTextTool(tool) ? Cursors.IBeam : Cursors.Cross;
        foreach (Image image in _visuals.Values) image.Cursor = tool is null ? Cursors.SizeAll : Surface.Cursor;
    }

    private void OnColorClick(object sender, RoutedEventArgs e)
    {
        if (CommitText() && !_suppressColorReopen) ColorPopup.IsOpen = true;
    }

    private void OnColorPopupClosed(object? sender, EventArgs e)
    {
        _suppressColorReopen = true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,
            new Action(() => _suppressColorReopen = false));
    }

    private void UpdateArrow(PhysicalPoint end)
    {
        if (_arrowStart is not PhysicalPoint start || _tool is null) return;
        if (start.DistanceTo(end) < 3)
        {
            _arrowDraft = null;
            if (_draftVisual is not null) Surface.Children.Remove(_draftVisual);
            _draftVisual = null;
            return;
        }
        _arrowDraft = CollageDecorationRenderer.CreateItem(Definition(start, end));
        if (_draftVisual is null)
        {
            _draftVisual = new Image { IsHitTestVisible = false, Stretch = Stretch.Fill };
            Surface.Children.Add(_draftVisual);
        }
        _draftVisual.Source = new DrawingImage(CollageDecorationRenderer.Draw(_arrowDraft));
        _draftVisual.Width = _arrowDraft.Bounds.Width;
        _draftVisual.Height = _arrowDraft.Bounds.Height;
        Canvas.SetLeft(_draftVisual, _arrowDraft.Bounds.X);
        Canvas.SetTop(_draftVisual, _arrowDraft.Bounds.Y);
    }

    private void BeginText(PhysicalPoint point)
    {
        _textDefinition = Definition(point, point);
        _textSize = SizeSlider.Value;
        var brush = new SolidColorBrush(Color.FromArgb(_color.A, _color.R, _color.G, _color.B));
        var editor = new TextBox
        {
            MinWidth = 40, FontFamily = new FontFamily(_textDefinition.FontFamily), FontSize = _textSize,
            FontWeight = _textDefinition.Bold ? FontWeights.Bold : FontWeights.Normal,
            FontStyle = _textDefinition.Italic ? FontStyles.Italic : FontStyles.Normal,
            MaxLength = _tool == CollageDecorationKind.Step ? 4 : 1000,
            Foreground = brush, CaretBrush = brush, Background = Brushes.White,
            Padding = new Thickness(0), BorderThickness = new Thickness(0),
            AcceptsReturn = false, AcceptsTab = false,
        };
        _textEditor = editor;
        Canvas.SetLeft(editor, point.X);
        Canvas.SetTop(editor, point.Y);
        Surface.Children.Add(editor);
        editor.PreviewKeyDown += (_, e) =>
        {
            if (e.Key is Key.Enter or Key.Escape) { e.Handled = true; CommitText(); }
        };
        editor.LostKeyboardFocus += (_, _) => CommitText();
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
        {
            if (_textEditor == editor) Keyboard.Focus(editor);
        }));
    }

    private bool CommitText()
    {
        if (_textEditor is not TextBox editor) return true;
        if (!string.IsNullOrWhiteSpace(editor.Text))
        {
            var item = CollageDecorationRenderer.CreateItem(_textDefinition! with { Text = editor.Text });
            try { _selected = _document.AddDecoration(item.Decoration!, item.Bounds); }
            catch (ArgumentOutOfRangeException ex) { ReportError(ex, Strings.CollageAnnotationError); return false; }
        }
        _textEditor = null;
        Surface.Children.Remove(editor);
        Refresh();
        SetTool(null);
        Keyboard.Focus(this);
        return true;
    }

    private static void SetChoices(ComboBox control, params string[] choices)
    {
        int selected = Math.Max(0, control.SelectedIndex);
        control.ItemsSource = choices;
        control.SelectedIndex = selected;
    }

    private void RefreshLanguage()
    {
        Title = Heading.Text = Strings.CollageTitle;
        GeneralHeading.Text = Strings.CollageGeneral;
        ZoomInButton.Content = Strings.CollageZoomIn;
        ZoomOutButton.Content = Strings.CollageZoomOut;
        ZoomActualButton.Content = Strings.CollageZoomActual;
        ZoomFitButton.Content = Strings.CollageZoomFit;
        ZoomHelp.Text = Strings.CollageZoomHelp;
        StampTool.Content = Strings.CollageStamps;
        StepTool.Content = Strings.CollageSteps;
        LabelTool.Content = Strings.CollageLabels;
        ShadowCheck.Content = Strings.AnnotationShadow;
        BoldCheck.Content = Strings.CollageBold;
        ItalicCheck.Content = Strings.CollageItalic;
        OutlineCheck.Content = Strings.CollageTextOutline;
        DashedCheck.Content = Strings.CollageDashed;
        OpacityLabel.Text = Strings.CollageOpacity;
        FontChoice.ToolTip = Strings.CollageFont;
        SetChoices(StampChoice, Strings.StampCheck, Strings.StampCross, Strings.StampProhibited,
            Strings.StampPlus, Strings.StampMinus, Strings.StampExclamation, Strings.StampQuestion);
        SetChoices(StepChoice, Strings.StepCircle, Strings.StepTeardrop);
        SetChoices(LabelChoice, Strings.LabelBox, Strings.LabelSpeech);
        SetChoices(TipChoice, Strings.ArrowTriangular, Strings.ArrowOpen, Strings.ArrowDouble);
        PropertiesHeading.Text = Strings.CollageProperties;
        PointerHint.Text = Strings.CollagePointerHint;
        BufferHeading.Text = Strings.CollageBuffer;
        BufferHint.Text = Strings.CollageBufferHint;
        BufferEmpty.Text = Strings.CollageBufferEmpty;
        ArrowTool.Content = Strings.CollageArrows;
        RefreshCaptureStrip();
        Hint.Text = Strings.CollageHint;
        CaptureButton.Content = Strings.CollageCapture;
        RemoveButton.Content = Strings.CollageRemove;
        UndoButton.Content = Strings.CollageUndo;
        RedoButton.Content = Strings.CollageRedo;
        ClearButton.Content = Strings.CollageClear;
        CopyButton.Content = Strings.CollageCopy;
        SaveButton.Content = Strings.CollageSave;
        HideButton.Content = Strings.CollageHide;
        ZoomLabel.Text = Strings.CollageZoom;
        PointerTool.Content = Strings.CollagePointer;
        CurvedTool.Content = Strings.CollageCurvedArrow;
        StraightTool.Content = Strings.CollageStraightArrow;
        ElbowTool.Content = Strings.CollageElbowArrow;
        TextTool.Content = Strings.CollageText;
        ColorButton.ToolTip = Strings.ColorPicker;
        SizeLabel.Text = IsTextTool(_tool) ? Strings.LabelTextSize : Strings.CollageObjectSize;
    }

    private void Refresh()
    {
        Surface.Children.Clear();
        _visuals.Clear();
        foreach (CollageItem item in _document.ItemsInPaintOrder)
        {
            var image = new Image
            {
                Source = item.Decoration is null ? _images[item.Id] : new DrawingImage(CollageDecorationRenderer.Draw(item)),
                Width = item.Bounds.Width, Height = item.Bounds.Height,
                Stretch = Stretch.Fill, Tag = item.Id, Cursor = _tool is null ? Cursors.SizeAll : Surface.Cursor,
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
            Canvas.SetLeft(image, item.Bounds.X);
            Canvas.SetTop(image, item.Bounds.Y);
            Surface.Children.Add(image);
            _visuals.Add(item.Id, image);
        }

        if (!_document.Items.Any(i => i.Id == _selected)) _selected = null;
        _selectionFrame = new Rectangle { Stroke = Brushes.DodgerBlue, StrokeThickness = 2 / ZoomSlider.Value, IsHitTestVisible = false };
        Surface.Children.Add(_selectionFrame);
        UpdateSelectionFrame();
        var bounds = _document.Bounds;
        // Extra workspace is editing chrome; export trims to the union of crop bounds.
        Surface.Width = Math.Max(1200, bounds.Right + 600);
        Surface.Height = Math.Max(800, bounds.Bottom + 600);
        Status.Text = string.Format(CultureInfo.CurrentCulture, Strings.CollageStatus, _document.Items.Count, bounds.Width, bounds.Height);
        ZoomValue.Text = ZoomSlider.Value.ToString("P0", CultureInfo.CurrentCulture);
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        CaptureButton.IsEnabled = HideButton.IsEnabled = !_exporting;
        RemoveButton.IsEnabled = !_exporting && _selected.HasValue;
        UndoButton.IsEnabled = !_exporting && _document.CanUndo;
        RedoButton.IsEnabled = !_exporting && _document.CanRedo;
        ClearButton.IsEnabled = CopyButton.IsEnabled = SaveButton.IsEnabled = !_exporting && _document.Items.Count > 0;
        Surface.IsEnabled = !_exporting;
        DrawingTools.IsEnabled = !_exporting;
        ToolProperties.IsEnabled = !_exporting;
        CaptureStrip.IsEnabled = !_exporting;
    }

    private void UpdateSelectionFrame()
    {
        if (_selectionFrame is null) return;
        bool selected = _selected.HasValue && _visuals.ContainsKey(_selected.Value);
        _selectionFrame.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
        if (!selected) return;
        Image image = _visuals[_selected!.Value];
        Canvas.SetLeft(_selectionFrame, Canvas.GetLeft(image));
        Canvas.SetTop(_selectionFrame, Canvas.GetTop(image));
        _selectionFrame.Width = image.Width;
        _selectionFrame.Height = image.Height;
    }

    private void OnSurfaceDown(object sender, MouseButtonEventArgs e)
    {
        if (_textEditor?.IsMouseOver == true) return;
        if (!CommitText()) return;
        Focus();
        Point position = e.GetPosition(Surface);
        if (_tool is not null)
        {
            _selected = null;
            UpdateSelectionFrame();
            var point = new PhysicalPoint(Math.Round(position.X), Math.Round(position.Y));
            if (IsTextTool(_tool)) BeginText(point);
            else if (_tool == CollageDecorationKind.Stamp)
            {
                var item = CollageDecorationRenderer.CreateItem(Definition(point, point));
                try { _selected = _document.AddDecoration(item.Decoration!, item.Bounds); }
                catch (ArgumentOutOfRangeException ex) { ReportError(ex, Strings.CollageAnnotationError); }
                Refresh();
                SetTool(null);
            }
            else
            {
                _arrowStart = point;
                Surface.CaptureMouse();
            }
            e.Handled = true;
            return;
        }
        if (e.OriginalSource is Image { Tag: Guid id })
        {
            _selected = id;
            _dragItem = _document.Items.Single(i => i.Id == id);
            _dragStart = e.GetPosition(Surface);
            _pendingX = _dragItem.Bounds.X;
            _pendingY = _dragItem.Bounds.Y;
            Surface.CaptureMouse();
        }
        else _selected = null;
        UpdateSelectionFrame();
        UpdateButtons();
        e.Handled = true;
    }

    private void OnSurfaceMove(object sender, MouseEventArgs e)
    {
        if (_arrowStart is not null)
        {
            Point current = e.GetPosition(Surface);
            UpdateArrow(new PhysicalPoint(Math.Clamp(Math.Round(current.X), 0, Surface.Width),
                Math.Clamp(Math.Round(current.Y), 0, Surface.Height)));
            return;
        }
        if (_dragItem is null) return;
        Point point = e.GetPosition(Surface);
        int x = (int)Math.Clamp(Math.Round(_dragItem.Bounds.X + point.X - _dragStart.X), 0, CollageDocument.MaxDimension);
        int y = (int)Math.Clamp(Math.Round(_dragItem.Bounds.Y + point.Y - _dragStart.Y), 0, CollageDocument.MaxDimension);
        if (!_document.CanMove(_dragItem.Id, x, y)) return;
        _pendingX = x;
        _pendingY = y;
        Canvas.SetLeft(_visuals[_dragItem.Id], x);
        Canvas.SetTop(_visuals[_dragItem.Id], y);
        UpdateSelectionFrame();
    }

    private void OnSurfaceUp(object sender, MouseButtonEventArgs e)
    {
        OnSurfaceMove(sender, e);
        FinishDrag();
    }
    private void OnSurfaceLostCapture(object sender, MouseEventArgs e) => FinishDrag();

    private void FinishDrag()
    {
        if (_arrowStart is not null)
        {
            _arrowStart = null;
            var draft = _arrowDraft;
            _arrowDraft = null;
            _draftVisual = null;
            Surface.ReleaseMouseCapture();
            if (draft is not null)
            {
                try { _selected = _document.AddDecoration(draft.Decoration!, draft.Bounds); }
                catch (ArgumentOutOfRangeException ex) { ReportError(ex, Strings.CollageAnnotationError); }
            }
            Refresh();
            if (draft is not null) SetTool(null);
        }
        if (_dragItem is null) return;
        var item = _dragItem;
        _dragItem = null;
        _document.Move(item.Id, _pendingX, _pendingY);
        Surface.ReleaseMouseCapture();
        Refresh();
    }

    private void OnZoomOut(object sender, RoutedEventArgs e) => ChangeZoom(ZoomSlider.Value / 1.25);
    private void OnZoomIn(object sender, RoutedEventArgs e) => ChangeZoom(ZoomSlider.Value * 1.25);
    private void OnZoomActual(object sender, RoutedEventArgs e) => ChangeZoom(1);
    private void OnZoomFit(object sender, RoutedEventArgs e)
    {
        if (!PrepareForCapture() || _document.Items.Count == 0) return;
        var bounds = _document.Bounds;
        ChangeZoom(Math.Min(Math.Max(1, Viewport.ViewportWidth - 24) / bounds.Width,
            Math.Max(1, Viewport.ViewportHeight - 24) / bounds.Height));
        Viewport.UpdateLayout();
        Viewport.ScrollToHorizontalOffset(Math.Max(0, bounds.X * ZoomSlider.Value - 12));
        Viewport.ScrollToVerticalOffset(Math.Max(0, bounds.Y * ZoomSlider.Value - 12));
    }
    private void ChangeZoom(double value)
    {
        if (_exporting || !PrepareForCapture()) return;
        ZoomSlider.Value = Math.Clamp(value, ZoomSlider.Minimum, ZoomSlider.Maximum);
    }
    private void OnViewportWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        ChangeZoom(ZoomSlider.Value * (e.Delta > 0 ? 1.25 : 0.8));
        e.Handled = true;
    }

    private void NudgeSelected(Key key, int distance)
    {
        FinishDrag();
        if (_selected is not Guid id) return;
        var item = _document.Items.Single(i => i.Id == id);
        int x = item.Bounds.X + (key == Key.Left ? -distance : key == Key.Right ? distance : 0);
        int y = item.Bounds.Y + (key == Key.Up ? -distance : key == Key.Down ? distance : 0);
        if (!_document.CanMove(id, x, y)) return;
        _document.Move(id, x, y);
        Refresh();
    }

    private void OnZoomChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ZoomTransform is null) return;
        ZoomTransform.ScaleX = ZoomTransform.ScaleY = e.NewValue;
        if (ZoomValue is not null) ZoomValue.Text = e.NewValue.ToString("P0", CultureInfo.CurrentCulture);
        if (_selectionFrame is not null) _selectionFrame.StrokeThickness = 2 / e.NewValue;
    }

    private void OnCapture(object sender, RoutedEventArgs e) { if (PrepareForCapture()) CaptureRequested?.Invoke(); }
    private void OnHide(object sender, RoutedEventArgs e) { if (PrepareForCapture()) Hide(); }
    private void OnRemove(object sender, RoutedEventArgs e)
    {
        FinishDrag();
        if (!CommitText()) return;
        if (_selected is Guid id) _document.Remove(id);
        Refresh();
    }
    private void OnUndo(object sender, RoutedEventArgs e) { FinishDrag(); if (!CommitText()) return; _document.Undo(); Refresh(); }
    private void OnRedo(object sender, RoutedEventArgs e) { FinishDrag(); if (!CommitText()) return; _document.Redo(); Refresh(); }
    private void OnClear(object sender, RoutedEventArgs e) { FinishDrag(); if (!CommitText()) return; _document.Clear(); Refresh(); }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (_exporting) return;
        if (_textEditor is not null || Keyboard.FocusedElement is TextBox) return;
        if (ColorPopup.IsOpen && e.Key == Key.Escape) { ColorPopup.IsOpen = false; e.Handled = true; return; }
        if ((Keyboard.Modifiers == ModifierKeys.None || Keyboard.Modifiers == ModifierKeys.Shift)
            && e.Key is Key.Left or Key.Right or Key.Up or Key.Down
            && Keyboard.FocusedElement is not (ComboBox or Slider))
        {
            NudgeSelected(e.Key, Keyboard.Modifiers == ModifierKeys.Shift ? 10 : 1);
            e.Handled = true;
            return;
        }
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.Z: OnUndo(this, e); break;
                case Key.Y: OnRedo(this, e); break;
                case Key.C: OnCopy(this, e); break;
                case Key.S: OnSave(this, e); break;
                default: return;
            }
        }
        else if (Keyboard.Modifiers == ModifierKeys.None && e.Key is Key.Delete or Key.Back) OnRemove(this, e);
        else if (e.Key == Key.Escape) OnHide(this, e);
        else return;
        e.Handled = true;
    }

    private async void OnCopy(object sender, RoutedEventArgs e)
    {
        if (_exporting || !CommitText()) return;
        FinishDrag();
        if (_document.Items.Count == 0) return;
        _exporting = true;
        UpdateButtons();
        try
        {
            var image = CollageComposer.Compose(_document, _images);
            for (int attempt = 0; ; attempt++)
            {
                try { Clipboard.SetImage(image); break; }
                catch (COMException) when (attempt < 4) { await Task.Delay(50); }
            }
            Status.Text = Strings.CollageExportComplete;
        }
        catch (Exception ex) { ReportError(ex, Strings.CollageCopyError); }
        finally { _exporting = false; UpdateButtons(); }
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (_exporting || !CommitText()) return;
        FinishDrag();
        if (_document.Items.Count == 0) return;
        _exporting = true;
        UpdateButtons();
        try
        {
            var options = _exportOptions();
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = Strings.SaveDialogTitle,
                FileName = ExportNaming.DefaultFileName(DateTime.Now, options.Format),
                DefaultExt = ExportNaming.ExtensionFor(options.Format), AddExtension = true, OverwritePrompt = true,
                Filter = $"{Strings.SaveFilterPng}|*.png|{Strings.SaveFilterJpeg}|*.jpg;*.jpeg",
                FilterIndex = options.Format == ImageExportFormat.Jpeg ? 2 : 1,
            };
            if (dialog.ShowDialog(this) != true) return;
            ImageExporter.Save(CollageComposer.Compose(_document, _images), dialog.FileName,
                ExportNaming.FormatFromExtension(dialog.FileName), options.Quality);
            Status.Text = Strings.CollageExportComplete;
        }
        catch (Exception ex) { ReportError(ex, Strings.SaveErrorMessage); }
        finally { _exporting = false; UpdateButtons(); }
    }

    private void ReportError(Exception ex, string message)
    {
        Trace.TraceError($"Zass: collage export failed: {ex}");
        MessageBox.Show(this, message, Strings.CollageTitle, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
