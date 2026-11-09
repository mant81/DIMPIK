using System.Drawing.Drawing2D;
using DIMPIK.Models;
using DIMPIK.Services;

namespace DIMPIK.UI;

internal sealed class DocumentTabStrip : Control
{
    private const int TabWidth = 278;
    private const int SideWidth = 32;
    private const int PlusWidth = 38;
    private readonly TabControl tabs;
    private int firstTab;
    private readonly ToolTip tip = new();
    public event EventHandler<int>? CloseRequested;
    public event EventHandler? CaptureRequested;

    public DocumentTabStrip(TabControl tabs)
    {
        this.tabs = tabs;
        Height = 40; BackColor = Color.FromArgb(237, 243, 252);
        AccessibleName = "캡처 탭";
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        tabs.SelectedIndexChanged += (_, _) => { EnsureSelectedVisible(); Invalidate(); };
        tabs.ControlAdded += (_, _) => { EnsureSelectedVisible(); Invalidate(); };
        tabs.ControlRemoved += (_, _) => { EnsureSelectedVisible(); Invalidate(); };
        tabs.Invalidated += (_, _) => Invalidate();
        tip.SetToolTip(this, "탭을 클릭해 전환하고 ×를 눌러 닫습니다.");
        Disposed += (_, _) => tip.Dispose();
    }

    private bool Overflow => tabs.TabCount * TabWidth + PlusWidth + 16 > ClientSize.Width;
    private int AvailableWidth => Math.Max(0, ClientSize.Width - (Overflow ? 2 * SideWidth : 0) - PlusWidth - 12);
    private int VisibleCount => Math.Max(1, AvailableWidth / TabWidth);
    private int StartX => Overflow ? SideWidth : 8;
    private Rectangle PlusBounds => new(ClientSize.Width - PlusWidth - (Overflow ? SideWidth : 4), 4, PlusWidth, 33);
    private Rectangle LeftBounds => new(0, 4, SideWidth, 33);
    private Rectangle RightBounds => new(ClientSize.Width - SideWidth, 4, SideWidth, 33);

    private void EnsureSelectedVisible()
    {
        firstTab = Math.Clamp(firstTab, 0, Math.Max(0, tabs.TabCount - VisibleCount));
        if (tabs.SelectedIndex < firstTab) firstTab = Math.Max(0, tabs.SelectedIndex);
        else if (tabs.SelectedIndex >= firstTab + VisibleCount) firstTab = tabs.SelectedIndex - VisibleCount + 1;
    }

    protected override void OnResize(EventArgs e) { base.OnResize(e); EnsureSelectedVisible(); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(BackColor);
        using var baseline = new Pen(Color.FromArgb(204, 219, 240));
        g.DrawLine(baseline, 0, Height - 1, Width, Height - 1);
        var state = g.Save();
        g.SetClip(new Rectangle(StartX, 0, AvailableWidth, Height));
        for (int i = firstTab; i < tabs.TabCount && (!Overflow || i < firstTab + VisibleCount); i++)
        {
            int x = StartX + (i - firstTab) * TabWidth;
            if (x >= StartX + AvailableWidth) break;
            bool active = i == tabs.SelectedIndex;
            var card = new Rectangle(x, 2, TabWidth - 5, Height - 3);
            using var path = new GraphicsPath();
            const int r = 8;
            path.AddArc(card.Left, card.Top, r * 2, r * 2, 180, 90);
            path.AddArc(card.Right - r * 2, card.Top, r * 2, r * 2, 270, 90);
            path.AddLine(card.Right, card.Top + r, card.Right, card.Bottom);
            path.AddLine(card.Right, card.Bottom, card.Left, card.Bottom); path.CloseFigure();
            using var fill = new SolidBrush(active ? Color.FromArgb(231, 240, 255) : Color.FromArgb(247, 250, 255));
            using var border = new Pen(active ? Color.FromArgb(37, 99, 235) : Color.FromArgb(203, 220, 242));
            g.FillPath(fill, path); g.DrawPath(border, path);
            Color ink = active ? Color.FromArgb(37, 99, 235) : Color.FromArgb(30, 41, 59);
            ChromeButton.DrawIcon(g, "image", new Rectangle(x + 12, 11, 18, 18), ink);
            var document = (CaptureDocument)tabs.TabPages[i].Tag!;
            using var font = new Font("Segoe UI", 8.5f, active ? FontStyle.Bold : FontStyle.Regular);
            TextRenderer.DrawText(g, CaptureNames.TabTitle(document.Name) + (document.Dirty ? " *" : ""), font,
                new Rectangle(x + 36, 0, TabWidth - 68, Height), ink,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, "×", Font, new Rectangle(x + TabWidth - 30, 0, 22, Height), ink,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
        }
        g.Restore(state);
        // The plus follows the last tab until the strip overflows, then stays reachable at the right.
        var plus = PlusRectangle();
        using var plusFill = new SolidBrush(Color.FromArgb(247, 250, 255));
        g.FillRectangle(plusFill, plus); g.DrawRectangle(baseline, plus);
        ChromeButton.DrawIcon(g, "plus", new Rectangle(plus.X + 9, plus.Y + 7, 20, 20), Color.FromArgb(30, 41, 59));
        if (Overflow)
        {
            TextRenderer.DrawText(g, "‹", Font, LeftBounds, Color.FromArgb(51, 65, 85), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, "›", Font, RightBounds, Color.FromArgb(51, 65, 85), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    private Rectangle PlusRectangle() => Overflow ? PlusBounds : new Rectangle(Math.Min(ClientSize.Width - PlusWidth, StartX + tabs.TabCount * TabWidth), 4, PlusWidth, 33);

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
        int within = (e.X - StartX) % TabWidth;
        if (within >= TabWidth - 32 && within < TabWidth - 4) CloseRequested?.Invoke(this, index);
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
