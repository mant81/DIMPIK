using System.Runtime.InteropServices;

namespace DIMPIK.UI;

internal sealed class EditorTitleBar : Panel
{
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    private readonly Form owner;
    public EditorTitleBar(Form owner)
    {
        this.owner = owner;
        Dock = DockStyle.Top; BackColor = Color.FromArgb(218, 234, 254);
        Height = PreferredHeight;
        DoubleBuffered = true;
        SetStyle(ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, true);
        var version = typeof(EditorTitleBar).Assembly.GetName().Version;
        var brand = new Label { Text = version is null ? "딤픽" : $"딤픽 v{version.ToString(3)}", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("맑은 고딕", 10.5f, FontStyle.Regular), ForeColor = Color.Black };
        var mark = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, Image = owner.Icon?.ToBitmap() };
        Disposed += (_, _) => { mark.Image?.Dispose(); brand.Font.Dispose(); };
        void Maximize() { owner.WindowState = owner.WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; }
        bool pendingDrag = false;
        Point dragStart = Point.Empty;
        foreach (Control caption in new Control[] { this, brand, mark })
        {
            caption.MouseDown += (_, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                if (e.Clicks > 1) { pendingDrag = false; Maximize(); return; }
                pendingDrag = true;
                dragStart = caption.PointToScreen(e.Location);
            };
            caption.MouseUp += (_, _) => pendingDrag = false;
            caption.MouseMove += (_, e) =>
            {
                if (!pendingDrag || (MouseButtons & MouseButtons.Left) == 0) return;
                var current = caption.PointToScreen(e.Location);
                var threshold = SystemInformation.DragSize;
                if (Math.Abs(current.X - dragStart.X) < threshold.Width / 2 &&
                    Math.Abs(current.Y - dragStart.Y) < threshold.Height / 2) return;
                pendingDrag = false;
                ReleaseCapture();
                SendMessage(owner.Handle, 0xA1, (IntPtr)2,
                    (IntPtr)unchecked((current.Y << 16) | (current.X & 0xffff)));
            };
        }
        var minimize = Command("−", "최소화", () => owner.WindowState = FormWindowState.Minimized);
        var maximize = Command("□", "최대화 / 복원", Maximize);
        var close = Command("×", "닫기", owner.Close);
        Controls.AddRange([brand, mark, minimize, maximize, close]);
        void LayoutTitle() {
            // The form owns the bar height. During a DPI transition, fit its children to
            // the actual height rather than changing the height again from Resize.
            float scale = Math.Min(DeviceDpi / 96f, Math.Max(1, ClientSize.Height) / 32f);
            int S(int value) => Math.Max(1, (int)Math.Round(value * scale));
            int iconSize = S(22);
            mark.SetBounds(S(20), (Height - iconSize) / 2, iconSize, iconSize); brand.SetBounds(S(52), 0, Math.Max(0, Width - S(190)), Height);
            close.SetBounds(Width-S(46), 0, S(46), Height); maximize.SetBounds(Width-S(92), 0, S(46), Height); minimize.SetBounds(Width-S(138), 0, S(46), Height);
        }
        Resize += (_, _) => LayoutTitle(); DpiChangedAfterParent += (_, _) => LayoutTitle(); LayoutTitle();
    }
    internal int PreferredHeight => Math.Max(1, (int)Math.Round(32 * DeviceDpi / 96f));
    private Button Command(string text, string name, Action action)
    {
        var button = new WindowControlButton(text) { AccessibleName = name, FlatStyle = FlatStyle.Flat,
            BackColor = BackColor, ForeColor = Color.Black, TabStop = false };
        button.FlatAppearance.BorderSize = 0; button.Click += (_, _) => action(); return button;
    }

    private sealed class WindowControlButton(string glyph) : Button
    {
        private bool hovered, pressed;
        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = e.Button == MouseButtons.Left; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            // The native flat-button painter adds a dark focus/default border.
            e.Graphics.Clear(pressed ? Color.FromArgb(187, 213, 248)
                : hovered || Focused && ShowFocusCues ? Color.FromArgb(205, 225, 252) : BackColor);
            float scale = DeviceDpi / 96f;
            float size = 10 * scale;
            float cx = (ClientSize.Width - 1) / 2f, cy = (ClientSize.Height - 1) / 2f;
            float half = size / 2;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var pen = new Pen(ForeColor, Math.Max(1f, scale));
            if (glyph == "−") e.Graphics.DrawLine(pen, cx - half, cy, cx + half, cy);
            else if (glyph == "□") e.Graphics.DrawRectangle(pen, cx - half, cy - half, size, size);
            else
            {
                e.Graphics.DrawLine(pen, cx - half, cy - half, cx + half, cy + half);
                e.Graphics.DrawLine(pen, cx + half, cy - half, cx - half, cy + half);
            }
        }
    }
}
