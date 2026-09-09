using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Zass.Core.Annotations;
using Zass.Core.Collage;
using Zass.Interop;

namespace Zass.App.Imaging;

internal static class CollageDecorationRenderer
{
    public static CollageItem CreateItem(CollageDecoration definition)
    {
        Drawing drawing = DrawContent(definition);
        Rect bounds = drawing.Bounds;
        int x = Math.Max(0, (int)Math.Floor(bounds.X));
        int y = Math.Max(0, (int)Math.Floor(bounds.Y));
        int right = (int)Math.Ceiling(bounds.Right);
        int bottom = (int)Math.Ceiling(bounds.Bottom);
        return new CollageItem(Guid.NewGuid(), new PhysicalRect(x, y, Math.Max(1, right - x), Math.Max(1, bottom - y)),
            definition with { From = definition.From.Offset(-x, -y), To = definition.To.Offset(-x, -y) });
    }

    public static Drawing Draw(CollageItem item)
    {
        var group = new DrawingGroup();
        var rect = new Rect(0, 0, item.Bounds.Width, item.Bounds.Height);
        group.ClipGeometry = new RectangleGeometry(rect);
        // Transparent bounds keep preview and export coordinates identical, including text whitespace.
        group.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(rect)));
        group.Children.Add(DrawContent(item.Decoration!));
        group.Freeze();
        return group;
    }

    private static Drawing DrawContent(CollageDecoration d)
    {
        var brush = new SolidColorBrush(Color.FromArgb(d.Color.A, d.Color.R, d.Color.G, d.Color.B));
        if (d.Kind == CollageDecorationKind.Text)
        {
            var text = new FormattedText(d.Text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), d.Size, brush, 1);
            var group = new DrawingGroup();
            using (DrawingContext dc = group.Open()) dc.DrawText(text, new Point(d.From.X, d.From.Y));
            return group;
        }
        return new GeometryDrawing(brush, null, BuildArrow(d));
    }

    private static Geometry BuildArrow(CollageDecoration d)
    {
        Point start = new(d.From.X, d.From.Y);
        Point end = new(d.To.X, d.To.Y);
        Vector delta = end - start;
        double length = delta.Length;
        if (length < 1) return Geometry.Empty;
        Vector axis = delta / length;
        Vector normal = new(-axis.Y, axis.X);
        double width = Math.Min(d.Size, length / 8);
        var points = new List<Point>();

        if (d.Kind == CollageDecorationKind.CurvedArrow)
        {
            Point control = start + delta * 0.5 + normal * (length * 0.42);
            const double join = 0.78;
            Point Curve(double t) => new(
                (1-t)*(1-t)*start.X + 2*(1-t)*t*control.X + t*t*end.X,
                (1-t)*(1-t)*start.Y + 2*(1-t)*t*control.Y + t*t*end.Y);
            Vector Side(double t)
            {
                Vector tangent = (control-start)*(2*(1-t)) + (end-control)*(2*t);
                tangent.Normalize();
                return new Vector(-tangent.Y, tangent.X);
            }
            var upper = new List<Point>();
            var lower = new List<Point>();
            for (int i = 0; i <= 32; i++)
            {
                double t = join*i/32;
                double half = width * (t/join) * 0.8;
                upper.Add(Curve(t) + Side(t)*half);
                lower.Add(Curve(t) - Side(t)*half);
            }
            points.AddRange(upper);
            points.Add(Curve(join) + Side(join)*width*2.1);
            points.Add(end);
            points.Add(Curve(join) - Side(join)*width*2.1);
            lower.Reverse();
            points.AddRange(lower);
        }
        else if (d.Kind == CollageDecorationKind.ElbowArrow)
        {
            // Orthogonal shaft with a conventional head pointing exactly at the release point.
            Point Local(double x, double y) => start + axis*x + normal*y;
            double offset = Math.Max(width*3, length*0.18);
            double head = Math.Min(length*0.25, width*3.5);
            var shaft = new StreamGeometry();
            Vector direction = axis;
            Point neck = end-axis*head;
            using (var ctx = shaft.Open())
            {
                ctx.BeginFigure(start, false, false);
                if (Math.Abs(delta.X) > width*4 && Math.Abs(delta.Y) > width*4)
                {
                    direction = new Vector(0, Math.Sign(delta.Y));
                    neck = end-direction*Math.Min(head, Math.Abs(delta.Y)*0.6);
                    ctx.LineTo(new Point(end.X, start.Y), true, false);
                }
                else
                {
                    ctx.LineTo(Local(length*0.3, 0), true, false);
                    ctx.LineTo(Local(length*0.3, offset), true, false);
                    ctx.LineTo(Local(length-head*1.25, offset), true, false);
                    ctx.LineTo(Local(length-head*1.25, 0), true, false);
                }
                ctx.LineTo(neck, true, false);
            }
            Vector side = new(-direction.Y, direction.X);
            var tip = new StreamGeometry();
            using (var ctx = tip.Open())
            {
                ctx.BeginFigure(neck+side*width*1.8, true, true);
                ctx.LineTo(end, true, false);
                ctx.LineTo(neck-side*width*1.8, true, false);
            }
            var result = new CombinedGeometry(GeometryCombineMode.Union,
                shaft.GetWidenedPathGeometry(new Pen(Brushes.Black, width) { LineJoin = PenLineJoin.Miter }), tip);
            result.Freeze();
            return result;
        }
        else
        {
            Point neck = end - axis*Math.Min(length*0.4, width*3.5);
            points.AddRange([start+normal*width/2, neck+normal*width/2, neck+normal*width*1.8,
                end, neck-normal*width*1.8, neck-normal*width/2, start-normal*width/2]);
        }
        var geometry = new StreamGeometry();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            ctx.BeginFigure(points[0], true, true);
            ctx.PolyLineTo(points.Skip(1).ToArray(), true, true);
        }
        geometry.Freeze();
        return geometry;
    }
}
