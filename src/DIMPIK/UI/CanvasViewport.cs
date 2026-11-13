namespace DIMPIK.UI;

// Handle zoom over the whitespace as well as over the image itself.
internal sealed class CanvasViewport : Panel
{
    public event MouseEventHandler? ZoomRequested;

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if ((ModifierKeys & Keys.Control) != 0)
        {
            if (e is HandledMouseEventArgs handled) handled.Handled = true;
            if (e.Delta != 0) ZoomRequested?.Invoke(this, e);
            return;
        }
        base.OnMouseWheel(e);
    }
}
