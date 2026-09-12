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
        Drawing content = DrawBare(d);
        var group = new DrawingGroup { Opacity = Math.Clamp(d.Opacity, 0, 1) };
        if (d.HasShadow)
        {
            Drawing shadow = Monochrome(content);
            var offset = new DrawingGroup { Transform = new TranslateTransform(3, 3), Opacity = 0.32 };
            offset.Children.Add(shadow);
            group.Children.Add(offset);
        }
        group.Children.Add(content);
        return group;
    }

    private static Drawing Monochrome(Drawing drawing)
    {
        if (drawing is GeometryDrawing shape)
        {
            Pen? pen = shape.Pen?.Clone();
            if (pen is not null) pen.Brush = Brushes.Black;
            return new GeometryDrawing(shape.Brush is null ? null : Brushes.Black, pen, shape.Geometry);
        }
        var result = new DrawingGroup();
        if (drawing is DrawingGroup group)
            foreach (Drawing child in group.Children) result.Children.Add(Monochrome(child));
        return result;
    }

    private static FormattedText Format(CollageDecoration d, string value, Brush brush) => new(value,
        CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
        new Typeface(new FontFamily(d.FontFamily), d.Italic ? FontStyles.Italic : FontStyles.Normal,
            d.Bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal), d.Size, brush, 1);

    private static Geometry Polygon(params Point[] points)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(points[0], true, true);
        ctx.PolyLineTo(points.Skip(1).ToArray(), true, false);
        return geometry;
    }

    private static Drawing DrawBare(CollageDecoration d)
    {
        var brush = new SolidColorBrush(Color.FromArgb(d.Color.A, d.Color.R, d.Color.G, d.Color.B));
        Point origin = new(d.From.X, d.From.Y);
        if (d.Kind == CollageDecorationKind.Text)
        {
            Geometry glyphs = Format(d, d.Text, brush).BuildGeometry(origin);
            var text = new DrawingGroup();
            if (d.TextOutline)
                text.Children.Add(new GeometryDrawing(null, new Pen(Brushes.White, Math.Max(1, d.Size / 12)), glyphs));
            // Paint the fill last so the outline cannot cover thin, regular-weight glyphs.
            text.Children.Add(new GeometryDrawing(brush, null, glyphs));
            return text;
        }
        if (d.Kind == CollageDecorationKind.Stamp) return DrawStamp(d, brush);
        if (d.Kind is CollageDecorationKind.Step or CollageDecorationKind.Label)
        {
            var group = new DrawingGroup();
            var text = Format(d, d.Text, brush);
            double pad = Math.Max(6, d.Size * 0.4);
            double width = text.WidthIncludingTrailingWhitespace + pad * 2;
            double height = text.Height + pad * 2;
            Geometry frame;
            Brush fill;
            Brush textBrush;
            if (d.Kind == CollageDecorationKind.Step)
            {
                width = height = Math.Max(Math.Max(width, height), d.Size * 1.9);
                double radius = width / 2;
                var circle = new EllipseGeometry(new Point(origin.X + radius, origin.Y + radius), radius, radius);
                frame = d.BadgeShape == CollageBadgeShape.Circle ? circle : new CombinedGeometry(GeometryCombineMode.Union,
                    circle, Polygon(new Point(origin.X + radius, origin.Y),
                        new Point(origin.X + width * 1.45, origin.Y + radius), new Point(origin.X + radius, origin.Y + height)));
                fill = brush;
                textBrush = Brushes.White;
            }
            else
            {
                var box = new RectangleGeometry(new Rect(origin.X, origin.Y, width, height), 3, 3);
                frame = d.LabelShape == CollageLabelShape.Box ? box : new CombinedGeometry(GeometryCombineMode.Union,
                    box, Polygon(new Point(origin.X + pad, origin.Y + height - 1),
                        new Point(origin.X + pad, origin.Y + height + pad),
                        new Point(origin.X + pad * 2.5, origin.Y + height - 1)));
                fill = Brushes.White;
                textBrush = brush;
            }
            group.Children.Add(new GeometryDrawing(fill, d.Kind == CollageDecorationKind.Label ? new Pen(brush, 2) : null, frame));
            var position = new Point(origin.X + (width - text.WidthIncludingTrailingWhitespace) / 2,
                origin.Y + (height - text.Height) / 2);
            group.Children.Add(new GeometryDrawing(textBrush, null, text.BuildGeometry(position)));
            return group;
        }
        if (d.Dashed || d.ArrowTip != CollageArrowTip.Filled) return DrawStyledArrow(d, brush);
        return new GeometryDrawing(brush, null, BuildArrow(d));
    }

    private static Drawing DrawStamp(CollageDecoration d, Brush brush)
    {
        double x = d.From.X, y = d.From.Y, size = d.Size * 1.8;
        var group = new DrawingGroup();
        var pen = new Pen(brush, Math.Max(2, size / 9)) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
        void Line(double x1, double y1, double x2, double y2) => group.Children.Add(new GeometryDrawing(null, pen,
            new LineGeometry(new Point(x + size*x1, y + size*y1), new Point(x + size*x2, y + size*y2))));
        switch (d.Stamp)
        {
            case CollageStamp.Check: Line(0.12,0.52,0.4,0.8); Line(0.4,0.8,0.9,0.16); break;
            case CollageStamp.Cross: Line(0.15,0.15,0.85,0.85); Line(0.85,0.15,0.15,0.85); break;
            case CollageStamp.Prohibited:
                group.Children.Add(new GeometryDrawing(null, pen, new EllipseGeometry(new Point(x+size/2,y+size/2),size*0.4,size*0.4)));
                Line(0.22,0.22,0.78,0.78); break;
            case CollageStamp.Plus: Line(0.1,0.5,0.9,0.5); Line(0.5,0.1,0.5,0.9); break;
            case CollageStamp.Minus: Line(0.1,0.5,0.9,0.5); break;
            default:
                var text = Format(d with { Size = size, Bold = true }, d.Stamp == CollageStamp.Exclamation ? "!" : "?", brush);
                group.Children.Add(new GeometryDrawing(brush, null, text.BuildGeometry(new Point(x,y)))); break;
        }
        return group;
    }

    private static Drawing DrawStyledArrow(CollageDecoration d, Brush brush)
    {
        Point from = new(d.From.X,d.From.Y), to = new(d.To.X,d.To.Y);
        Vector delta = to-from;
        if (delta.Length < 1) return new DrawingGroup();
        Vector axis = delta; axis.Normalize();
        Vector normal = new(-axis.Y,axis.X);
        double head = Math.Min(delta.Length*0.24,Math.Max(8,d.Size*2.5));
        Point control = from + delta*0.5 + normal*delta.Length*0.42;
        Point elbow = new(to.X,from.Y);
        Vector endDirection = d.Kind == CollageDecorationKind.CurvedArrow ? to-control :
            d.Kind == CollageDecorationKind.ElbowArrow && Math.Abs(delta.Y)>1 ? to-elbow : delta;
        Vector startDirection = d.Kind == CollageDecorationKind.CurvedArrow ? control-from :
            d.Kind == CollageDecorationKind.ElbowArrow && Math.Abs(delta.X)>1 ? elbow-from : delta;
        endDirection.Normalize(); startDirection.Normalize();
        var line = new StreamGeometry();
        using(var ctx = line.Open())
        {
            ctx.BeginFigure(from, false, false);
            if(d.Kind == CollageDecorationKind.CurvedArrow) ctx.QuadraticBezierTo(control,to,true,false);
            else
            {
                if(d.Kind == CollageDecorationKind.ElbowArrow) ctx.LineTo(elbow,true,false);
                ctx.LineTo(to,true,false);
            }
        }
        var pen = new Pen(brush, Math.Max(1,d.Size*0.45)) { LineJoin=PenLineJoin.Miter };
        if(d.Dashed) pen.DashStyle=new DashStyle(new double[]{3,2},0);
        var group=new DrawingGroup();
        group.Children.Add(new GeometryDrawing(null,pen,line));
        void Tip(Point tip, Vector direction)
        {
            Vector side=new(-direction.Y,direction.X);
            Point neck=tip-direction*head;
            if(d.ArrowTip == CollageArrowTip.Open)
            {
                var path=new StreamGeometry();
                using(var ctx=path.Open())
                {
                    ctx.BeginFigure(neck+side*head*0.5,false,false);
                    ctx.LineTo(tip,true,false);ctx.LineTo(neck-side*head*0.5,true,false);
                }
                group.Children.Add(new GeometryDrawing(null,new Pen(brush,pen.Thickness),path));
            }
            else group.Children.Add(new GeometryDrawing(brush,null,Polygon(tip,neck+side*head*0.5,neck-side*head*0.5)));
        }
        Tip(to,endDirection);
        if(d.ArrowTip==CollageArrowTip.Double) Tip(from,-startDirection);
        return group;
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
