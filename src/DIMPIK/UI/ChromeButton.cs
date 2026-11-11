using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace DIMPIK.UI;

/// <summary>A compact, keyboard-accessible button used by the editor chrome.</summary>
public sealed class ChromeButton : Button
{
    private bool hovered;
    private bool pressed;
    private bool selected;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string IconKey { get; set; } = "";
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Primary { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IconAboveText { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool TextAtBottom { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowDropArrow { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int IconSize { get; set; } = 20;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Selected { get => selected; set { if (selected == value) return; selected = value; Invalidate(); } }

    public ChromeButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Cursor = Cursors.Hand;
        Font = new Font("Segoe UI", 9f); ForeColor = Color.FromArgb(51, 65, 85);
        AccessibleRole = AccessibleRole.PushButton;
    }
    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    private int ScaleMetric(int value) => Math.Max(1, (int)Math.Round(value * DeviceDpi / 96f));

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(OpaqueBackground(this)); g.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
        var fill = Primary ? (pressed ? Color.FromArgb(29, 78, 216) : hovered ? Color.FromArgb(29, 78, 216) : Color.FromArgb(37, 99, 235))
            : Selected ? Color.FromArgb(196, 214, 255) : pressed ? Color.FromArgb(226, 232, 240) : hovered ? Color.FromArgb(231, 240, 255) : IconAboveText ? OpaqueBackground(this) : BackColor;
        using var path = Rounded(bounds, Math.Min(ScaleMetric(8), Math.Min(Width, Height) / 3));
        using var background = new SolidBrush(fill); g.FillPath(background, path);
        if (Selected || Focused && ShowFocusCues)
        {
            using var border = new Pen(Selected ? Color.FromArgb(147, 197, 253) : Color.FromArgb(37, 99, 235)); g.DrawPath(border, path);
        }
        var foreground = !Enabled ? Color.FromArgb(148, 163, 184) : Primary ? Color.White : Selected ? Color.FromArgb(37, 99, 235) : ForeColor;
        bool hasIcon = !string.IsNullOrEmpty(IconKey);
        int iconSize = Math.Min(ScaleMetric(IconSize), Math.Max(ScaleMetric(12), Height - ScaleMetric(12)));
        if (!hasIcon && TextAtBottom)
        {
            TextRenderer.DrawText(g, Text, Font, new Rectangle(0, Math.Max(0, Height - ScaleMetric(18)), Width, Math.Min(ScaleMetric(18), Height)), foreground,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        }
        else if (IconAboveText && hasIcon)
        {
            bool arrowBesideIcon = ShowDropArrow && string.IsNullOrEmpty(Text);
            int iconY = string.IsNullOrEmpty(Text) ? (Height - iconSize) / 2 : ScaleMetric(7);
            DrawIcon(g, IconKey, new Rectangle((Width - iconSize - (arrowBesideIcon ? ScaleMetric(8) : 0)) / 2, iconY, iconSize, iconSize), foreground);
            var textBounds = new Rectangle(ScaleMetric(2), iconSize + ScaleMetric(8), Width - ScaleMetric(4), Math.Max(0, Height - iconSize - ScaleMetric(10)));
            if (ShowDropArrow && !string.IsNullOrEmpty(Text))
            {
                int textWidth = Math.Min(Width - ScaleMetric(16), TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width);
                int textLeft = (Width - textWidth - ScaleMetric(10)) / 2;
                textBounds = new Rectangle(textLeft, textBounds.Y, textWidth, textBounds.Height);
                using var arrow = new SolidBrush(foreground);
                int x = textBounds.Right + ScaleMetric(6), y = textBounds.Top + textBounds.Height / 2;
                g.FillPolygon(arrow, [new Point(x - ScaleMetric(3), y - ScaleMetric(2)), new Point(x + ScaleMetric(3), y - ScaleMetric(2)), new Point(x, y + ScaleMetric(2))]);
            }
            TextRenderer.DrawText(g, Text, Font, textBounds, foreground,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }
        else
        {
            int iconX = string.IsNullOrEmpty(Text) ? (Width - iconSize) / 2 : ScaleMetric(11);
            if (hasIcon) DrawIcon(g, IconKey, new Rectangle(iconX, (Height - iconSize) / 2, iconSize, iconSize), foreground);
            int left = hasIcon ? iconX + iconSize + ScaleMetric(7) : 0;
            TextRenderer.DrawText(g, Text, Font, new Rectangle(left, 0, Math.Max(0, Width - left - ScaleMetric(6)), Height), foreground,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | (hasIcon ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter) | TextFormatFlags.NoPadding);
        }
        if (ShowDropArrow && (!IconAboveText || string.IsNullOrEmpty(Text)))
        {
            using var arrow = new SolidBrush(foreground);
            int x = Width - ScaleMetric(12), y = IconAboveText ? ScaleMetric(7) + iconSize / 2 : Height / 2;
            g.FillPolygon(arrow, [new Point(x - ScaleMetric(4), y - ScaleMetric(3)), new Point(x + ScaleMetric(4), y - ScaleMetric(3)), new Point(x, y + ScaleMetric(3))]);
        }
    }

    internal static Color OpaqueBackground(Control control)
    {
        for (var parent = control.Parent; parent != null; parent = parent.Parent)
            if (parent.BackColor.A == 255) return parent.BackColor;
        return SystemColors.Control;
    }

    private static GraphicsPath Rounded(Rectangle r, int radius)
    {
        var path = new GraphicsPath(); int d = Math.Max(2, radius * 2);
        path.AddArc(r.Left, r.Top, d, d, 180, 90); path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
    }
    private static Color HueColor(int hue)
    {
        double h = (hue % 360) / 60d, x = 1 - Math.Abs(h % 2 - 1);
        (double r, double g, double b) = h switch
        {
            < 1 => (1d, x, 0d), < 2 => (x, 1d, 0d), < 3 => (0d, 1d, x),
            < 4 => (0d, x, 1d), < 5 => (x, 0d, 1d), _ => (1d, 0d, x)
        };
        return Color.FromArgb((int)(r * 255), (int)(g * 255), (int)(b * 255));
    }
    internal static void DrawIcon(Graphics g, string key, Rectangle r, Color color, float strokeWidth = 1.8f)
    {
        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TranslateTransform(r.X, r.Y); g.ScaleTransform(r.Width / 20f, r.Height / 20f);
        using var pen = new Pen(color, strokeWidth) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var brush = new SolidBrush(color);
        switch (key)
        {
            case "rotate-right":
                g.DrawArc(pen, 3, 3, 14, 14, 35, 300);
                g.DrawLines(pen, [new Point(14, 2), new Point(18, 3), new Point(17, 7)]);
                break;
            case "crop": g.DrawLines(pen, [new Point(5, 1), new Point(5, 15), new Point(19, 15)]); g.DrawLines(pen, [new Point(1, 5), new Point(15, 5), new Point(15, 19)]); break;
            case "resize": g.DrawRectangle(pen, 3, 3, 14, 14); g.DrawLine(pen, 7, 13, 15, 5); g.DrawLines(pen, [new Point(10, 5), new Point(15, 5), new Point(15, 10)]); break;
            case "region-select":
                pen.DashStyle = DashStyle.Dash;
                g.DrawRectangle(pen, 3, 3, 14, 14);
                break;
            case "region-copy":
                pen.DashStyle = DashStyle.Dot;
                using (var selection = Rounded(new Rectangle(3, 3, 14, 14), 2)) g.DrawPath(pen, selection);
                break;
            case "capture": pen.DashStyle = DashStyle.Dash; g.DrawRectangle(pen, 3, 3, 14, 14); break;
            case "monitor": g.DrawRectangle(pen, 2, 3, 16, 11); g.DrawLine(pen, 10, 14, 10, 17); g.DrawLine(pen, 6, 18, 14, 18); break;
            case "select": g.DrawPolygon(pen, [new Point(4, 2), new Point(4, 15), new Point(7, 12), new Point(10, 18), new Point(13, 16), new Point(10, 11), new Point(15, 11)]); break;
            case "arrow": g.DrawLine(pen, 3, 17, 17, 3); g.DrawLines(pen, [new Point(9, 3), new Point(17, 3), new Point(17, 11)]); break;
            case "shape": g.DrawEllipse(pen, 2, 2, 11, 11); g.DrawRectangle(pen, 8, 8, 10, 10); break;
            case "pen": g.DrawPolygon(pen, [new Point(3, 15), new Point(12, 3), new Point(17, 8), new Point(7, 18), new Point(2, 18)]); g.DrawLine(pen, 10, 6, 14, 10); break;
            case "rectangle": g.DrawRectangle(pen, 3, 4, 14, 12); break;
            case "ellipse": g.DrawEllipse(pen, 3, 3, 14, 14); break;
            case "rounded-rectangle":
                using (var rounded = new GraphicsPath())
                {
                    rounded.AddArc(3, 4, 6, 6, 180, 90); rounded.AddArc(11, 4, 6, 6, 270, 90);
                    rounded.AddArc(11, 10, 6, 6, 0, 90); rounded.AddArc(3, 10, 6, 6, 90, 90); rounded.CloseFigure();
                    g.DrawPath(pen, rounded);
                }
                break;
            case "line": g.DrawLine(pen, 3, 17, 17, 3); break;
            case "line-width":
                for (int i = 0; i < 3; i++)
                {
                    float y = 5 + i * 5;
                    g.DrawBezier(pen, 2, y, 6, y - 3, 8, y + 3, 11, y);
                    g.DrawBezier(pen, 11, y, 14, y - 3, 16, y + 3, 18, y);
                }
                break;
            case "triangle": g.DrawPolygon(pen, [new Point(10, 3), new Point(18, 17), new Point(2, 17)]); break;
            case "text": g.DrawLine(pen, 3, 4, 17, 4); g.DrawLine(pen, 10, 4, 10, 17); g.DrawLine(pen, 7, 17, 13, 17); break;
            case "number": g.DrawEllipse(pen, 2, 2, 16, 16); using (var f = new Font("Segoe UI", 9, FontStyle.Bold, GraphicsUnit.Pixel)) g.DrawString("1", f, brush, 7, 4); break;
            case "highlight": g.DrawPolygon(pen, [new Point(3, 14), new Point(12, 4), new Point(16, 8), new Point(7, 18), new Point(3, 18)]); g.DrawLine(pen, 2, 19, 18, 19); break;
            case "open": g.DrawLines(pen, [new Point(2, 6), new Point(2, 16), new Point(17, 16), new Point(19, 8), new Point(6, 8), new Point(4, 16)]); g.DrawLines(pen, [new Point(2, 6), new Point(7, 6), new Point(9, 8)]); break;
            case "save":
                g.DrawLines(pen, [new PointF(3, 1.8f), new PointF(14.2f, 1.8f), new PointF(17.8f, 5), new PointF(17.8f, 18.2f), new PointF(2.2f, 18.2f), new PointF(2.2f, 2.6f), new PointF(3, 1.8f)]);
                g.DrawLines(pen, [new PointF(6, 2), new PointF(6, 8), new PointF(14, 8), new PointF(14, 2)]);
                g.DrawRectangle(pen, 5.5f, 12, 9, 6);
                break;
            case "export":
                g.DrawLines(pen, [new PointF(12.5f, 5), new PointF(19, 5), new PointF(19, 11.5f)]);
                g.DrawLine(pen, 18.5f, 5.5f, 10.5f, 13.5f);
                g.DrawLines(pen, [new PointF(8.7f, 6.2f), new PointF(3.5f, 6.2f), new PointF(2.2f, 7.5f), new PointF(2.2f, 18.7f), new PointF(14.6f, 18.7f), new PointF(16, 17.4f), new PointF(16, 13.5f)]);
                break;
            case "copy": g.DrawRectangle(pen, 6, 5, 11, 13); g.DrawLines(pen, [new Point(3, 14), new Point(3, 2), new Point(13, 2)]); break;
            case "undo":
                g.DrawLines(pen, [new PointF(8, 4), new PointF(3, 8.5f), new PointF(8, 13)]);
                using (var undoPath = new GraphicsPath())
                {
                    undoPath.AddLine(4, 8.5f, 11.5f, 8.5f);
                    undoPath.AddBezier(11.5f, 8.5f, 15.2f, 8.5f, 17.2f, 10.2f, 17.2f, 13.5f);
                    undoPath.AddBezier(17.2f, 13.5f, 17.2f, 16.5f, 14.8f, 18, 11.8f, 18);
                    g.DrawPath(pen, undoPath);
                }
                break;
            case "redo":
                g.DrawLines(pen, [new PointF(12, 4), new PointF(17, 8.5f), new PointF(12, 13)]);
                using (var redoPath = new GraphicsPath())
                {
                    redoPath.AddLine(16, 8.5f, 8.5f, 8.5f);
                    redoPath.AddBezier(8.5f, 8.5f, 4.8f, 8.5f, 2.8f, 10.2f, 2.8f, 13.5f);
                    redoPath.AddBezier(2.8f, 13.5f, 2.8f, 16.5f, 5.2f, 18, 8.2f, 18);
                    g.DrawPath(pen, redoPath);
                }
                break;
            case "minus": g.DrawLine(pen, 4, 10, 16, 10); break;
            case "plus": g.DrawLine(pen, 4, 10, 16, 10); g.DrawLine(pen, 10, 4, 10, 16); break;
            case "zoom-minus":
                g.DrawEllipse(pen, 1.5f, 1.5f, 17, 17); g.DrawLine(pen, 6, 10, 14, 10);
                break;
            case "zoom-plus":
                g.DrawEllipse(pen, 1.5f, 1.5f, 17, 17); g.DrawLine(pen, 6, 10, 14, 10); g.DrawLine(pen, 10, 6, 10, 14);
                break;
            case "color-wheel":
                for (int i = 0; i < 12; i++)
                {
                    using var wheelPen = new Pen(HueColor(i * 30), 3.2f);
                    g.DrawArc(wheelPen, 3.5f, 3.5f, 13, 13, i * 30, 32);
                }
                using (var wheelBadge = new SolidBrush(Color.White)) g.FillEllipse(wheelBadge, 13, 1, 7, 7);
                using (var plusPen = new Pen(color, 1.1f))
                { g.DrawLine(plusPen, 16.5f, 2.5f, 16.5f, 6.5f); g.DrawLine(plusPen, 14.5f, 4.5f, 18.5f, 4.5f); }
                break;
            case "fit": g.DrawLines(pen, [new Point(3, 7), new Point(3, 3), new Point(7, 3)]); g.DrawLines(pen, [new Point(13, 3), new Point(17, 3), new Point(17, 7)]); g.DrawLines(pen, [new Point(17, 13), new Point(17, 17), new Point(13, 17)]); g.DrawLines(pen, [new Point(7, 17), new Point(3, 17), new Point(3, 13)]); break;
            case "fullscreen":
                g.DrawLines(pen, [new Point(1, 8), new Point(1, 1), new Point(8, 1)]);
                g.DrawLines(pen, [new Point(12, 1), new Point(19, 1), new Point(19, 8)]);
                g.DrawLines(pen, [new Point(19, 12), new Point(19, 19), new Point(12, 19)]);
                g.DrawLines(pen, [new Point(8, 19), new Point(1, 19), new Point(1, 12)]);
                break;
            case "image":
                using (var frame = Rounded(new Rectangle(2, 2, 16, 16), 2)) g.DrawPath(pen, frame);
                g.DrawEllipse(pen, 5, 5, 3, 3);
                g.DrawLines(pen, [new Point(4, 15), new Point(9, 10), new Point(12, 13), new Point(14, 10), new Point(17, 15)]);
                break;
            case "settings":
                g.DrawLine(pen, 10, 1.6f, 10, 4.2f); g.DrawLine(pen, 10, 15.8f, 10, 18.4f);
                g.DrawLine(pen, 1.6f, 10, 4.2f, 10); g.DrawLine(pen, 15.8f, 10, 18.4f, 10);
                g.DrawLine(pen, 15.95f, 4.05f, 14.1f, 5.9f); g.DrawLine(pen, 5.9f, 14.1f, 4.05f, 15.95f);
                g.DrawLine(pen, 15.95f, 15.95f, 14.1f, 14.1f); g.DrawLine(pen, 5.9f, 5.9f, 4.05f, 4.05f);
                g.DrawEllipse(pen, 4.5f, 4.5f, 11, 11); g.DrawEllipse(pen, 8, 8, 4, 4);
                break;
            case "help":
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var helpPen = new Pen(color, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                using (var helpBrush = new SolidBrush(color))
                using (var question = new GraphicsPath())
                {
                    g.DrawEllipse(helpPen, 2.1f, 2.1f, 15.8f, 15.8f);
                    question.AddBezier(7, 7, 7, 4, 13, 4, 13, 7);
                    question.AddBezier(13, 7, 13, 9.2f, 10, 9.2f, 10, 12);
                    g.DrawPath(helpPen, question);
                    g.FillEllipse(helpBrush, 9.15f, 15, 1.7f, 1.7f);
                }
                break;
            case "history": g.DrawEllipse(pen, 2.5f, 2.5f, 15, 15); g.DrawLine(pen, 10, 5, 10, 10); g.DrawLine(pen, 10, 10, 14, 12); g.DrawLines(pen, [new Point(2, 6), new Point(2, 2), new Point(6, 2)]); break;
            case "paste": g.DrawLines(pen, [new Point(7, 4), new Point(4, 4), new Point(4, 18), new Point(16, 18), new Point(16, 4), new Point(13, 4)]); g.DrawRectangle(pen, 7, 2, 6, 4); g.DrawLine(pen, 7, 10, 13, 10); g.DrawLine(pen, 7, 14, 12, 14); break;
            case "delete": g.DrawLine(pen, 3, 5, 17, 5); g.DrawLines(pen, [new Point(5, 5), new Point(6, 18), new Point(14, 18), new Point(15, 5)]); g.DrawLines(pen, [new Point(7, 5), new Point(7, 2), new Point(13, 2), new Point(13, 5)]); g.DrawLine(pen, 8, 8, 8, 14); g.DrawLine(pen, 12, 8, 12, 14); break;
            case "check": g.DrawLines(pen, [new Point(3, 10), new Point(8, 15), new Point(17, 5)]); break;
            case "close": g.DrawLine(pen, 5, 5, 15, 15); g.DrawLine(pen, 15, 5, 5, 15); break;
            case "mosaic": for (int y = 3; y < 18; y += 5) for (int x = 3; x < 18; x += 5) { if ((x + y) % 2 == 0) g.FillRectangle(brush, x, y, 4, 4); else g.DrawRectangle(pen, x, y, 3, 3); } break;
            case "blur":
                using (var drop = new GraphicsPath())
                {
                    drop.AddBezier(10, 2, 8, 5, 4, 9, 4, 13);
                    drop.AddBezier(4, 13, 4, 16.5f, 6.7f, 19, 10, 19);
                    drop.AddBezier(10, 19, 13.3f, 19, 16, 16.5f, 16, 13);
                    drop.AddBezier(16, 13, 16, 9, 12, 5, 10, 2);
                    g.DrawPath(pen, drop);
                }
                break;
            case "more": g.FillEllipse(brush, 3, 8, 3, 3); g.FillEllipse(brush, 8.5f, 8, 3, 3); g.FillEllipse(brush, 14, 8, 3, 3); break;
        }
        g.Restore(state);

    }

    internal static void DrawMenuIcon(Graphics graphics, string key, Rectangle bounds, Color color)
    {
        if (key == "save") { DrawIcon(graphics, key, bounds, color, 1.2f); return; }
        var state = graphics.Save();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TranslateTransform(bounds.X, bounds.Y);
        graphics.ScaleTransform(bounds.Width / 20f, bounds.Height / 20f);
        using var pen = new Pen(color, 1.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        switch (key)
        {
            case "export":
                using (var box = Rounded(new Rectangle(2, 5, 15, 13), 2)) graphics.DrawPath(pen, box);
                graphics.DrawLines(pen, [new PointF(12, 2), new PointF(18, 7), new PointF(12, 12)]);
                using (var arrow = new GraphicsPath())
                {
                    arrow.AddBezier(5, 14, 5, 8, 8, 7, 17, 7);
                    graphics.DrawPath(pen, arrow);
                }
                break;
            case "undo":
            case "redo":
                if (key == "redo") { graphics.TranslateTransform(20, 0); graphics.ScaleTransform(-1, 1); }
                graphics.DrawLines(pen, [new PointF(3, 2), new PointF(3, 7), new PointF(8, 7)]);
                using (var arrow = new GraphicsPath())
                {
                    arrow.AddBezier(3, 7, 5, 1, 16, 1, 16, 8);
                    arrow.AddBezier(16, 8, 16, 12, 11, 15, 7, 18);
                    graphics.DrawPath(pen, arrow);
                }
                break;
            case "settings":
                using (var gear = new GraphicsPath())
                {
                    var points = new PointF[32];
                    for (int i = 0; i < points.Length; i++)
                    {
                        double angle = (i - .5) * Math.PI / 16;
                        float radius = i % 4 is 0 or 1 ? 8.2f : 6.3f;
                        points[i] = new PointF(10 + radius * (float)Math.Cos(angle), 10 + radius * (float)Math.Sin(angle));
                    }
                    gear.AddPolygon(points); graphics.DrawPath(pen, gear);
                    graphics.DrawEllipse(pen, 7, 7, 6, 6);
                }
                break;
        }
        graphics.Restore(state);
    }
}

/// <summary>Small original mark drawn at any display scale.</summary>
public sealed class BrandMark : Control
{
    public BrandMark()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        AccessibleName = "DIMPIK"; AccessibleRole = AccessibleRole.Graphic;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        Draw(e.Graphics, Size);
    }
    public static void Draw(Graphics g, Size size)
    {
        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        float sx = size.Width / 100f, sy = size.Height / 100f;
        g.ScaleTransform(sx, sy);
        using var path = new GraphicsPath();
        path.AddArc(2, 2, 44, 44, 180, 90); path.AddArc(54, 2, 44, 44, 270, 90);
        path.AddArc(54, 54, 44, 44, 0, 90); path.AddArc(2, 54, 44, 44, 90, 90); path.CloseFigure();
        using (var background = new LinearGradientBrush(new Rectangle(0, 0, 100, 100), Color.FromArgb(8, 190, 255), Color.FromArgb(109, 38, 239), 40f))
            g.FillPath(background, path);
        g.SetClip(path, CombineMode.Intersect);
        using (var folded = new GraphicsPath())
        {
            folded.StartFigure(); folded.AddLine(55, 0, 100, 0); folded.AddLine(100, 0, 100, 39);
            folded.AddBezier(100, 39, 88, 25, 70, 28, 55, 0); folded.CloseFigure();
            using var foldFill = new LinearGradientBrush(new Rectangle(55, 0, 45, 40), Color.FromArgb(255, 63, 140), Color.FromArgb(255, 185, 27), 0f);
            g.FillPath(foldFill, folded);
        }
        using var pen = new Pen(Color.White, 7.5f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLines(pen, [new Point(21, 40), new Point(21, 25), new Point(39, 25)]);
        g.DrawLines(pen, [new Point(61, 25), new Point(79, 25), new Point(79, 40)]);
        g.DrawLines(pen, [new Point(21, 60), new Point(21, 75), new Point(39, 75)]);
        g.DrawLines(pen, [new Point(63, 75), new Point(79, 75), new Point(79, 60)]);
        using var pointer = new GraphicsPath();
        pointer.AddPolygon([new Point(44, 39), new Point(70, 56), new Point(59, 59), new Point(64, 68), new Point(58, 71), new Point(53, 61), new Point(46, 68)]);
        using (var pointerFill = new SolidBrush(Color.White)) g.FillPath(pointerFill, pointer);
        g.Restore(state);
    }
}

public sealed class ColorSwatch : Button
{
    private bool selected;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color SwatchColor { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsFillPicker { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Selected { get => selected; set { if (selected == value) return; selected = value; Invalidate(); } }
    public ColorSwatch()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Cursor = Cursors.Hand; AccessibleRole = AccessibleRole.PushButton;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(ChromeButton.OpaqueBackground(this)); g.SmoothingMode = SmoothingMode.AntiAlias;
        if (IsFillPicker)
        {
            using var border = new Pen(Color.FromArgb(203, 213, 225));
            using var background = new SolidBrush(Color.White);
            using var path = new GraphicsPath();
            var right = Math.Max(8, Width - 2); var bottom = Math.Max(8, Height - 2); const int radius = 10;
            path.AddArc(1, 1, radius, radius, 180, 90); path.AddArc(right - radius, 1, radius, radius, 270, 90);
            path.AddArc(right - radius, bottom - radius, radius, radius, 0, 90); path.AddArc(1, bottom - radius, radius, radius, 90, 90); path.CloseFigure();
            g.FillPath(background, path); g.DrawPath(border, path);
            using var previewBrush = new SolidBrush(SwatchColor); g.FillRectangle(previewBrush, 7, 5, Math.Max(1, Width - 32), Math.Max(1, Height - 10));
            using var arrow = new Pen(Color.FromArgb(71, 85, 105), 1.5f); g.DrawLines(arrow, [new Point(Width - 19, Height / 2 - 2), new Point(Width - 15, Height / 2 + 2), new Point(Width - 11, Height / 2 - 2)]);
            return;
        }
        int size = Math.Min(Width, Height); var outer = new Rectangle((Width - size) / 2 + 1, (Height - size) / 2 + 1, size - 3, size - 3);
        if (Selected || Focused && ShowFocusCues)
        {
            using var ring = new Pen(Color.FromArgb(37, 99, 235), 1.5f); g.DrawEllipse(ring, outer);
        }
        outer.Inflate(-2, -2);
        using var fill = new SolidBrush(SwatchColor); g.FillEllipse(fill, outer);
        using var edge = new Pen(Color.FromArgb(30, 15, 23, 42)); g.DrawEllipse(edge, outer);
    }
}
