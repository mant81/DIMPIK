namespace DIMPIK.Models;

public enum AnnotationKind { Rectangle, Arrow, Text, Number, Highlight, Image, PixelPatch, Ellipse, RoundedRectangle, Line, Triangle, Pen }

public sealed record Annotation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public AnnotationKind Kind { get; set; }
    public RectangleF Bounds { get; set; }
    public Color Color { get; set; } = Color.FromArgb(37, 99, 235);
    public Color? FillColor { get; set; }
    public float Width { get; set; } = 3;
    public string Text { get; set; } = "";

    public bool Filled { get; set; }
    public bool Dashed { get; set; }
    public float FontSize { get; set; } = 24;
    public int Rotation { get; set; }
    public bool Bold { get; set; }
    public bool ReverseX { get; set; }
    public bool ReverseY { get; set; }
    // Pen points are relative to Bounds, so moving or resizing the object preserves its path.
    public List<PointF> Points { get; set; } = [];
    // Encoded pixels are immutable and shared safely by undo snapshots.
    public string ImageData { get; init; } = "";
    public bool IsSelectable => Kind != AnnotationKind.PixelPatch;

    public Annotation Copy(bool newIdentity = false) => this with { Id = newIdentity ? Guid.NewGuid() : Id, Points = [.. Points] };

    public void Draw(Graphics g)
    {
        var graphicsState = g.Save();
        var b = Bounds;
        if (Rotation % 360 != 0)
        {
            int rotation = ((Rotation % 360) + 360) % 360;
            float localWidth = rotation is 90 or 270 ? b.Height : b.Width;
            float localHeight = rotation is 90 or 270 ? b.Width : b.Height;
            g.TranslateTransform(b.Left + b.Width / 2, b.Top + b.Height / 2);
            g.RotateTransform(rotation);
            b = new(-localWidth / 2, -localHeight / 2, localWidth, localHeight);
        }
        using var pen = new Pen(Color, Width) { DashStyle = Dashed ? System.Drawing.Drawing2D.DashStyle.Dash : System.Drawing.Drawing2D.DashStyle.Solid };
        using var brush = new SolidBrush(Color);
        try
        {
        switch (Kind)
        {
            case AnnotationKind.Image:
                using (var image = Services.ImageSafety.LoadBase64(ImageData)) g.DrawImage(image, b);
                break;
            case AnnotationKind.PixelPatch:
                var state = g.Save();
                try
                {
                    g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                    using var image = Services.ImageSafety.LoadBase64(ImageData); g.DrawImage(image, b);
                }
                finally { g.Restore(state); }
                break;
            case AnnotationKind.Rectangle:
                if (Filled) { using var fill = new SolidBrush(System.Drawing.Color.FromArgb(55, FillColor ?? Color)); g.FillRectangle(fill, b); }
                g.DrawRectangle(pen, b.X, b.Y, b.Width, b.Height); break;
            case AnnotationKind.Ellipse:
                if (Filled) { using var fill = new SolidBrush(System.Drawing.Color.FromArgb(55, FillColor ?? Color)); g.FillEllipse(fill, b); }
                g.DrawEllipse(pen, b); break;
            case AnnotationKind.RoundedRectangle:
                using (var rounded = RoundedRectangle(b, Math.Min(12, Math.Min(b.Width, b.Height) / 3)))
                {
                    if (Filled) { using var fill = new SolidBrush(System.Drawing.Color.FromArgb(55, FillColor ?? Color)); g.FillPath(fill, rounded); }
                    g.DrawPath(pen, rounded);
                }
                break;
            case AnnotationKind.Line:
                g.DrawLine(pen, ReverseX ? b.Right : b.Left, ReverseY ? b.Bottom : b.Top, ReverseX ? b.Left : b.Right, ReverseY ? b.Top : b.Bottom);
                break;
            case AnnotationKind.Pen:
                if (Points.Count >= 2)
                {
                    pen.StartCap = pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                    pen.LineJoin = System.Drawing.Drawing2D.LineJoin.Round;
                    g.DrawLines(pen, Points.Select(point => new PointF(b.X + point.X * b.Width, b.Y + point.Y * b.Height)).ToArray());
                }
                break;
            case AnnotationKind.Arrow:
                using (var cap = new System.Drawing.Drawing2D.AdjustableArrowCap(4, 5))
                {
                    pen.CustomEndCap = cap;
                    g.DrawLine(pen, ReverseX ? b.Right : b.Left, ReverseY ? b.Bottom : b.Top, ReverseX ? b.Left : b.Right, ReverseY ? b.Top : b.Bottom);
                }
                break;
            case AnnotationKind.Triangle:
                PointF[] triangle = [new(b.Left + b.Width / 2, b.Top), new(b.Right, b.Bottom), new(b.Left, b.Bottom)];
                if (Filled) { using var fill = new SolidBrush(System.Drawing.Color.FromArgb(55, FillColor ?? Color)); g.FillPolygon(fill, triangle); }
                g.DrawPolygon(pen, triangle);
                break;
            case AnnotationKind.Highlight:
                using (var translucent = new SolidBrush(System.Drawing.Color.FromArgb(70, Color))) g.FillRectangle(translucent, b);
                break;
            case AnnotationKind.Number:
                g.FillEllipse(brush, b);
                using (var font = new Font("Segoe UI", Math.Max(1, Math.Min(b.Height * .52f, b.Width * .95f / Math.Max(1, Text.Length))), FontStyle.Bold, GraphicsUnit.Pixel))
                using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString(Text, font, Brushes.White, b, format);
                break;
            case AnnotationKind.Text:
                using (var font = new Font("Segoe UI", FontSize, Bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel))
                using (var format = new StringFormat(StringFormat.GenericTypographic))
                    g.DrawString(Text, font, brush, b, format);
                break;
        }
        }
        finally { g.Restore(graphicsState); }
    }

    private static System.Drawing.Drawing2D.GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        float d = Math.Max(1, radius * 2);
        path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
