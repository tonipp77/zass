using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Zass.App.Resources;

namespace Zass.App.Overlay;

/// <summary>
/// A self-contained HSV color picker: a quick palette of common colors, a
/// saturation/value area for the active hue, a hue strip and a hex field. Pure
/// WPF, no external dependencies; lives on the overlay inside a popup. Works in
/// fixed pixel sizes so thumb positions are deterministic without a layout pass.
/// </summary>
public partial class ColorPicker : UserControl
{
    private const double SvWidth = 196.0;
    private const double SvHeight = 120.0;
    private const double HueWidth = 196.0;

    private static readonly Color[] Palette =
    {
        Color.FromRgb(0xE8, 0x11, 0x23), // red
        Color.FromRgb(0xF7, 0x63, 0x0C), // orange
        Color.FromRgb(0xFF, 0xB9, 0x00), // amber
        Color.FromRgb(0x10, 0x7C, 0x10), // green
        Color.FromRgb(0x00, 0xB7, 0xC3), // teal
        Color.FromRgb(0x00, 0x78, 0xD7), // blue
        Color.FromRgb(0x88, 0x6C, 0xE4), // violet
        Color.FromRgb(0xE3, 0x00, 0x8C), // magenta
        Color.FromRgb(0xFF, 0xFF, 0xFF), // white
        Color.FromRgb(0xC8, 0xC8, 0xC8), // light gray
        Color.FromRgb(0x80, 0x80, 0x80), // gray
        Color.FromRgb(0x00, 0x00, 0x00), // black
    };

    private double _hue;        // 0..360
    private double _saturation; // 0..1
    private double _value;      // 0..1

    public ColorPicker()
    {
        InitializeComponent();
        HexLabel.Text = Strings.LabelHex;
        BuildPalette();
        ApplyHsv(raiseChanged: false);
    }

    /// <summary>Raised on every change (including live drags), for live previews.</summary>
    public event EventHandler? SelectedColorChanged;

    /// <summary>Raised when the user picks a final color (palette swatch or hex Enter/Esc).</summary>
    public event EventHandler? ColorCommitted;

    /// <summary>The current color. Setting it updates the picker state and UI.</summary>
    public Color SelectedColor
    {
        get => ColorFromHsv(_hue, _saturation, _value);
        set
        {
            (_hue, _saturation, _value) = HsvFromColor(value);
            ApplyHsv(raiseChanged: false);
        }
    }

    private void BuildPalette()
    {
        foreach (Color color in Palette)
        {
            var swatch = new Border
            {
                Width = 22,
                Height = 22,
                Margin = new Thickness(2),
                CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(color),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
            };
            Color captured = color;
            swatch.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                SetColor(captured, commit: true);
            };
            PalettePanel.Children.Add(swatch);
        }
    }

    // --- Saturation / value area ---

    private void OnSvMouseDown(object sender, MouseButtonEventArgs e)
    {
        SvArea.CaptureMouse();
        UpdateSvFromPoint(e.GetPosition(SvArea));
    }

    private void OnSvMouseMove(object sender, MouseEventArgs e)
    {
        if (SvArea.IsMouseCaptured)
        {
            UpdateSvFromPoint(e.GetPosition(SvArea));
        }
    }

    private void OnSvMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (SvArea.IsMouseCaptured)
        {
            SvArea.ReleaseMouseCapture();
        }
    }

    private void UpdateSvFromPoint(Point p)
    {
        _saturation = Math.Clamp(p.X / SvWidth, 0, 1);
        _value = Math.Clamp(1 - (p.Y / SvHeight), 0, 1);
        ApplyHsv(raiseChanged: true);
    }

    // --- Hue strip ---

    private void OnHueMouseDown(object sender, MouseButtonEventArgs e)
    {
        HueStrip.CaptureMouse();
        UpdateHueFromPoint(e.GetPosition(HueStrip));
    }

    private void OnHueMouseMove(object sender, MouseEventArgs e)
    {
        if (HueStrip.IsMouseCaptured)
        {
            UpdateHueFromPoint(e.GetPosition(HueStrip));
        }
    }

    private void OnHueMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (HueStrip.IsMouseCaptured)
        {
            HueStrip.ReleaseMouseCapture();
        }
    }

    private void UpdateHueFromPoint(Point p)
    {
        _hue = Math.Clamp(p.X / HueWidth, 0, 1) * 360.0;
        ApplyHsv(raiseChanged: true);
    }

    // --- Hex field ---

    private void OnHexPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return)
        {
            e.Handled = true;
            TryApplyHex(commit: true);
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            ApplyHsv(raiseChanged: false); // revert any unparsed text
            ColorCommitted?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnHexLostFocus(object sender, KeyboardFocusChangedEventArgs e) => TryApplyHex(commit: false);

    private void TryApplyHex(bool commit)
    {
        if (TryParseHex(HexBox.Text, out Color color))
        {
            SetColor(color, commit);
        }
        else
        {
            ApplyHsv(raiseChanged: false); // restore the last valid value
            if (commit)
            {
                ColorCommitted?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private void SetColor(Color color, bool commit)
    {
        (_hue, _saturation, _value) = HsvFromColor(color);
        ApplyHsv(raiseChanged: true);
        if (commit)
        {
            ColorCommitted?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Pushes the current HSV state into the visuals and (optionally) notifies listeners.</summary>
    private void ApplyHsv(bool raiseChanged)
    {
        SvHueLayer.Fill = new SolidColorBrush(ColorFromHsv(_hue, 1, 1));

        Canvas.SetLeft(SvThumb, (_saturation * SvWidth) - (SvThumb.Width / 2));
        Canvas.SetTop(SvThumb, ((1 - _value) * SvHeight) - (SvThumb.Height / 2));
        Canvas.SetLeft(HueThumb, (_hue / 360.0 * HueWidth) - (HueThumb.Width / 2));

        HexBox.Text = ToHex(ColorFromHsv(_hue, _saturation, _value));

        if (raiseChanged)
        {
            SelectedColorChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // --- Color math ---

    private static string ToHex(Color c) =>
        string.Create(CultureInfo.InvariantCulture, $"#{c.R:X2}{c.G:X2}{c.B:X2}");

    private static bool TryParseHex(string? text, out Color color)
    {
        color = Colors.Black;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string hex = text.Trim().TrimStart('#');
        if (hex.Length != 6 ||
            !byte.TryParse(hex.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r) ||
            !byte.TryParse(hex.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g) ||
            !byte.TryParse(hex.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
        {
            return false;
        }

        color = Color.FromRgb(r, g, b);
        return true;
    }

    private static Color ColorFromHsv(double h, double s, double v)
    {
        h = (((h % 360) + 360) % 360) / 60.0;
        double c = v * s;
        double x = c * (1 - Math.Abs((h % 2) - 1));
        double m = v - c;

        double r1, g1, b1;
        if (h < 1) { r1 = c; g1 = x; b1 = 0; }
        else if (h < 2) { r1 = x; g1 = c; b1 = 0; }
        else if (h < 3) { r1 = 0; g1 = c; b1 = x; }
        else if (h < 4) { r1 = 0; g1 = x; b1 = c; }
        else if (h < 5) { r1 = x; g1 = 0; b1 = c; }
        else { r1 = c; g1 = 0; b1 = x; }

        return Color.FromRgb(
            (byte)Math.Round((r1 + m) * 255),
            (byte)Math.Round((g1 + m) * 255),
            (byte)Math.Round((b1 + m) * 255));
    }

    private static (double Hue, double Saturation, double Value) HsvFromColor(Color color)
    {
        double r = color.R / 255.0;
        double g = color.G / 255.0;
        double b = color.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        double hue = 0;
        if (delta > 0)
        {
            if (max == r)
            {
                hue = 60 * (((g - b) / delta) % 6);
            }
            else if (max == g)
            {
                hue = 60 * (((b - r) / delta) + 2);
            }
            else
            {
                hue = 60 * (((r - g) / delta) + 4);
            }
        }

        if (hue < 0)
        {
            hue += 360;
        }

        double saturation = max == 0 ? 0 : delta / max;
        return (hue, saturation, max);
    }
}
