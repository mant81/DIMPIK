using System.Runtime.InteropServices;

namespace DIMPIK.UI;

// Observe edge input without overlay controls or a visible native frame.
internal sealed class WindowResizeBorder : IMessageFilter, IDisposable
{
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr window, ref Point point);
    private readonly Form owner;
    private Point startMouse;
    private Rectangle startBounds;
    private int direction;
    private bool resizing;
    public WindowResizeBorder(Form owner) { this.owner = owner; Application.AddMessageFilter(this); }
    public void Dispose() { Application.RemoveMessageFilter(this); }
    public bool PreFilterMessage(ref Message m)
    {
        if (owner.IsDisposed || !owner.IsHandleCreated || owner.WindowState != FormWindowState.Normal) return false;
        if (m.Msg is not (0x200 or 0x201 or 0x202)) return false;
        if (!resizing && m.HWnd != owner.Handle && !owner.Contains(Control.FromChildHandle(m.HWnd))) return false;
        var screen = new Point(unchecked((short)m.LParam.ToInt64()), unchecked((short)(m.LParam.ToInt64() >> 16)));
        ClientToScreen(m.HWnd, ref screen);
        var p = owner.PointToClient(screen);
        int edge = Math.Max(4, (int)Math.Round(5 * owner.DeviceDpi / 96f));
        int hit = (p.X < edge ? 1 : p.X >= owner.ClientSize.Width - edge ? 2 : 0)
            | (p.Y < edge ? 4 : p.Y >= owner.ClientSize.Height - edge ? 8 : 0);
        if (m.Msg == 0x201 && hit != 0)
        {
            startMouse = screen; startBounds = owner.Bounds; direction = hit;
            resizing = true; owner.Capture = true; return true;
        }
        if (m.Msg == 0x202 && resizing) { resizing = false; owner.Capture = false; return true; }
        if (m.Msg != 0x200) return false;
        if (!resizing)
        {
            if (hit == 0) return false;
            Cursor.Current = ResizeCursor(hit); return true;
        }
        Cursor.Current = ResizeCursor(direction);
        int dx = screen.X - startMouse.X, dy = screen.Y - startMouse.Y;
        var bounds = startBounds;
        if ((direction & 1) != 0) { int width = Math.Max(owner.MinimumSize.Width, startBounds.Width - dx); bounds.X = startBounds.Right - width; bounds.Width = width; }
        if ((direction & 2) != 0) bounds.Width = Math.Max(owner.MinimumSize.Width, startBounds.Width + dx);
        if ((direction & 4) != 0) { int height = Math.Max(owner.MinimumSize.Height, startBounds.Height - dy); bounds.Y = startBounds.Bottom - height; bounds.Height = height; }
        if ((direction & 8) != 0) bounds.Height = Math.Max(owner.MinimumSize.Height, startBounds.Height + dy);
        owner.Bounds = bounds; return true;
    }
    private static Cursor ResizeCursor(int hit) => hit is 5 or 10 ? Cursors.SizeNWSE : hit is 6 or 9 ? Cursors.SizeNESW : (hit & 3) != 0 ? Cursors.SizeWE : Cursors.SizeNS;
}
