namespace DIMPIK.UI;

internal sealed class EditorMenuRenderer : ToolStripSystemRenderer
{
    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        if (e.Item.Owner is not MenuStrip) { base.OnRenderSeparator(e); return; }
        float scale = (e.Item.Owner.DeviceDpi) / 96f;
        int length = Math.Min(e.Item.Height, (int)Math.Round(24 * scale));
        int top = (e.Item.Height - length) / 2;
        int x = (e.Item.Width - 1) / 2;
        using var line = new Pen(Color.FromArgb(190, 209, 238));
        e.Graphics.DrawLine(line, x, top, x, top + length - 1);
    }

    protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Enabled || !e.Item.Selected && !e.Item.Pressed) return;
        using var fill = new SolidBrush(e.Item.Pressed ? Color.FromArgb(202, 221, 250) : Color.FromArgb(218, 232, 253));
        e.Graphics.FillRectangle(fill, new Rectangle(Point.Empty, e.Item.Size));
    }
}
