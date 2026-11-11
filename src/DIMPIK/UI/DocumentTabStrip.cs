using System.Drawing.Drawing2D;
using DIMPIK.Models;
using DIMPIK.Services;

namespace DIMPIK.UI;

internal sealed class DocumentTabStrip : Control
{
    private const int LogicalTabWidth = 146;
    private const int LogicalSideWidth = 32;
    private const int LogicalPlusWidth = 40;
    private readonly TabControl tabs;
    private int firstTab;
    private readonly ToolTip tip = new();
    public event EventHandler<int>? CloseRequested;
    public event EventHandler? CaptureRequested;

    public DocumentTabStrip(TabControl tabs)
    {
        this.tabs = tabs;
        Height = PreferredHeight; BackColor = Color.FromArgb(237, 243, 252);
        AccessibleName = "캡처 탭";
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        tabs.SelectedIndexChanged += (_, _) => { EnsureSelectedVisible(); Invalidate(); };
        tabs.ControlAdded += (_, _) => { EnsureSelectedVisible(); Invalidate(); };
        tabs.ControlRemoved += (_, _) => { EnsureSelectedVisible(); Invalidate(); };
        tabs.Invalidated += (_, _) => Invalidate();
        tip.SetToolTip(this, "탭을 클릭해 전환하고 ×를 눌러 닫습니다.");
        Disposed += (_, _) => tip.Dispose();
    }

    private int Scale(int logical) => Math.Max(1, (int)Math.Round(logical * DeviceDpi / 96f));
    public int PreferredHeight => Scale(36);
    private int TabWidth => Scale(LogicalTabWidth);
    private int SideWidth => Scale(LogicalSideWidth);
    private int PlusWidth => Scale(LogicalPlusWidth);
    private bool Overflow => tabs.TabCount * TabWidth + PlusWidth + Scale(28) > ClientSize.Width;
    private int AvailableWidth => Math.Max(0, ClientSize.Width - StartX - (Overflow ? SideWidth : Scale(4)) - PlusWidth);
    private int VisibleCount => Math.Max(1, AvailableWidth / TabWidth);
    private int StartX => Overflow ? SideWidth : Scale(18);
    private Rectangle PlusBounds => new(ClientSize.Width - PlusWidth - (Overflow ? SideWidth : Scale(4)), Scale(3), PlusWidth, Scale(38));
    private Rectangle LeftBounds => new(0, Scale(3), SideWidth, Scale(38));
    private Rectangle RightBounds => new(ClientSize.Width - SideWidth, Scale(3), SideWidth, Scale(38));

    private void EnsureSelectedVisible()
    {
        firstTab = Math.Clamp(firstTab, 0, Math.Max(0, tabs.TabCount - VisibleCount));
        if (tabs.SelectedIndex < firstTab) firstTab = Math.Max(0, tabs.SelectedIndex);
        else if (tabs.SelectedIndex >= firstTab + VisibleCount) firstTab = tabs.SelectedIndex - VisibleCount + 1;
    }

    protected override void OnResize(EventArgs e) { base.OnResize(e); EnsureSelectedVisible(); Invalidate(); }
    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        Height = PreferredHeight;
        EnsureSelectedVisible();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(BackColor);
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
        using var stripFill = new LinearGradientBrush(ClientRectangle,
            Color.FromArgb(235, 242, 255), Color.FromArgb(232, 240, 253), LinearGradientMode.Horizontal);
        g.FillRectangle(stripFill, ClientRectangle);
        using var lowerBand = new SolidBrush(Color.FromArgb(205, 227, 253));
        g.FillRectangle(lowerBand, 0, Height - Scale(3), Width, Scale(3));
        using var baseline = new Pen(Color.FromArgb(204, 219, 240));
        using var topEdge = new Pen(Color.FromArgb(216, 226, 243));
        g.DrawLine(topEdge, 0, 0, Width, 0);
        g.DrawLine(baseline, 0, Height - Scale(1), Width, Height - Scale(1));
        var state = g.Save();
        g.SetClip(new Rectangle(StartX, 0, AvailableWidth, Height));
        for (int i = firstTab; i < tabs.TabCount && (!Overflow || i < firstTab + VisibleCount); i++)
        {
            int x = StartX + (i - firstTab) * TabWidth;
            if (x >= StartX + AvailableWidth) break;
            bool active = i == tabs.SelectedIndex;
            int top = Scale(active ? 1 : 3);
            var card = new Rectangle(x, top, TabWidth - Scale(4), Height - top);
            using var path = new GraphicsPath();
            int r = Scale(10);
            path.AddLine(card.Left, card.Bottom, card.Left, card.Top + r);
            path.AddArc(card.Left, card.Top, r * 2, r * 2, 180, 90);
            path.AddArc(card.Right - r * 2, card.Top, r * 2, r * 2, 270, 90);
            path.AddLine(card.Right, card.Top + r, card.Right, card.Bottom);
            using var fill = new LinearGradientBrush(card, Color.FromArgb(248, 251, 255),
                active ? Color.FromArgb(222, 235, 255) : Color.FromArgb(244, 247, 253), LinearGradientMode.Vertical);
            using var edgeBrush = new LinearGradientBrush(card,
                active ? Color.FromArgb(62, 76, 255) : Color.FromArgb(210, 220, 235),
                Color.FromArgb(210, 224, 247), LinearGradientMode.Vertical);
            using var border = new Pen(edgeBrush, active ? Math.Max(1f, DeviceDpi / 96f * 1.2f) : 1f);
            g.FillPath(fill, path); g.DrawPath(border, path);
            Color ink = active ? Color.FromArgb(36, 43, 245) : Color.FromArgb(30, 41, 59);
            ChromeButton.DrawIcon(g, "image", new Rectangle(x + Scale(12), Scale(11), Scale(16), Scale(16)), ink);
            var document = (CaptureDocument)tabs.TabPages[i].Tag!;
            using var font = new Font("Segoe UI", active ? 9.5f : 9f, active ? FontStyle.Bold : FontStyle.Regular);
            TextRenderer.DrawText(g, CaptureNames.TabTitle(document.Name) + (document.Dirty ? " *" : ""), font,
                new Rectangle(x + Scale(36), Scale(2), TabWidth - Scale(68), Height - Scale(2)), ink,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            var close = CloseBounds(x);
            int cx = close.Left + close.Width / 2, cy = close.Top + close.Height / 2;
            int half = Scale(5);
            using var closePen = new Pen(ink, Math.Max(1f, DeviceDpi / 96f * 1.5f));
            g.DrawLine(closePen, cx - half, cy - half, cx + half, cy + half);
            g.DrawLine(closePen, cx + half, cy - half, cx - half, cy + half);
        }
        g.Restore(state);
        // The plus follows the last tab until the strip overflows, then stays reachable at the right.
        var plus = PlusRectangle();
        using var plusFill = new SolidBrush(Color.FromArgb(247, 250, 255));
        using var plusPath = RoundedPanel.CreatePath(plus, Scale(10));
        g.FillPath(plusFill, plusPath); g.DrawPath(baseline, plusPath);
        ChromeButton.DrawIcon(g, "plus", new Rectangle(plus.X + Scale(11), plus.Y + Scale(8), Scale(18), Scale(18)), Color.FromArgb(30, 41, 59));
        if (Overflow)
        {
            TextRenderer.DrawText(g, "‹", Font, LeftBounds, Color.FromArgb(51, 65, 85), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, "›", Font, RightBounds, Color.FromArgb(51, 65, 85), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    private Rectangle PlusRectangle() => Overflow ? PlusBounds : new Rectangle(Math.Min(ClientSize.Width - PlusWidth, StartX + tabs.TabCount * TabWidth), Scale(3), PlusWidth, Scale(32));

    private Rectangle CloseBounds(int x) => new(x + TabWidth - Scale(37), 0, Scale(32), Height);

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        if (PlusRectangle().Contains(e.Location)) { CaptureRequested?.Invoke(this, EventArgs.Empty); return; }
        if (Overflow && LeftBounds.Contains(e.Location)) { firstTab = Math.Max(0, firstTab - 1); Invalidate(); return; }
        if (Overflow && RightBounds.Contains(e.Location)) { firstTab = Math.Min(Math.Max(0, tabs.TabCount - VisibleCount), firstTab + 1); Invalidate(); return; }
        if (e.X < StartX || e.X >= StartX + AvailableWidth) return;
        int index = firstTab + (e.X - StartX) / TabWidth;
        if (index < 0 || index >= tabs.TabCount || (Overflow && index >= firstTab + VisibleCount)) return;
        int x = StartX + (index - firstTab) * TabWidth;
        if (e.X >= x + TabWidth - Scale(5)) return;
        if (CloseBounds(x).Contains(e.Location)) CloseRequested?.Invoke(this, index);
        else tabs.SelectedIndex = index;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (!Overflow) return;
        firstTab = Math.Clamp(firstTab + (e.Delta < 0 ? 1 : -1), 0, Math.Max(0, tabs.TabCount - VisibleCount));
        Invalidate();
    }
}
