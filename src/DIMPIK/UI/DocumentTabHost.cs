namespace DIMPIK.UI;

// Let the document cover the native TabControl frame; the editor paints its own chrome.
internal sealed class DocumentTabHost : TabControl
{
    public override Rectangle DisplayRectangle
    {
        get
        {
            var display = base.DisplayRectangle;
            return new Rectangle(0, display.Top, ClientSize.Width, Math.Max(0, ClientSize.Height - display.Top));
        }
    }
}

internal sealed class EditorStatusRenderer : ToolStripProfessionalRenderer
{
    protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e) => RenderHighlight(e);

    protected override void OnRenderDropDownButtonBackground(ToolStripItemRenderEventArgs e) => RenderHighlight(e);

    private static void RenderHighlight(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Enabled || !e.Item.Selected && !e.Item.Pressed) return;
        float scale = (e.Item.Owner?.DeviceDpi ?? 96) / 96f;
        int inset = Math.Max(1, (int)Math.Round(2 * scale));
        var bounds = new RectangleF(inset, inset,
            e.Item.Width - inset * 2, e.Item.Height - inset * 2);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        var smoothing = e.Graphics.SmoothingMode;
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var shape = RoundedPanel.CreatePath(bounds, 6 * scale);
        using var fill = new SolidBrush(e.Item.Pressed ? Color.FromArgb(202, 221, 250) : Color.FromArgb(218, 232, 253));
        e.Graphics.FillPath(fill, shape);
        e.Graphics.SmoothingMode = smoothing;
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        float scale = (e.Item.Owner?.DeviceDpi ?? 96) / 96f;
        int length = Math.Min(e.Item.Height, (int)Math.Round(20 * scale));
        int top = (e.Item.Height - length) / 2;
        using var separator = new Pen(Color.FromArgb(190, 209, 238));
        int x = Math.Max(0, (e.Item.Width - 1) / 2);
        e.Graphics.DrawLine(separator, x, top, x, top + length - 1);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var boundary = new Pen(Color.FromArgb(248, 251, 255));
        e.Graphics.DrawLine(boundary, 0, 0, e.ToolStrip.Width, 0);
    }
}
