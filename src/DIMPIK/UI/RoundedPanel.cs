using System.Drawing.Drawing2D;

namespace DIMPIK.UI;

/// <summary>A lightly bordered surface for the editor toolbar.</summary>
public sealed class RoundedPanel : Panel
{
    public RoundedPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.FromArgb(252, 253, 255);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? SystemColors.Control);
        if (Width < 4 || Height < 4) return;
        using var path = CreatePath(new RectangleF(1, 1, Width - 3, Height - 4), 14);
        using var shadow = new SolidBrush(Color.FromArgb(14, 51, 65, 85));
        using (var shadowPath = (GraphicsPath)path.Clone())
        using (var offset = new Matrix())
        {
            offset.Translate(0, 2); shadowPath.Transform(offset); e.Graphics.FillPath(shadow, shadowPath);
        }
        using var fill = new SolidBrush(BackColor);
        using var border = new Pen(Color.FromArgb(229, 235, 245));
        e.Graphics.FillPath(fill, path); e.Graphics.DrawPath(border, path);
    }

    internal static GraphicsPath CreatePath(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        float diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
