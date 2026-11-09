namespace DIMPIK.UI;

internal sealed class BufferedFlowLayoutPanel : FlowLayoutPanel
{
    public BufferedFlowLayoutPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
    }
}
