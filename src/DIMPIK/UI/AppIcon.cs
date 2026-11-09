namespace DIMPIK.UI;

internal static class AppIcon
{
    public static Icon Load()
    {
        using var stream = typeof(AppIcon).Assembly.GetManifestResourceStream("DIMPIK.Assets.DIMPIK.ico")
            ?? throw new InvalidOperationException("DIMPIK 아이콘 리소스가 없습니다.");
        using var icon = new Icon(stream, new Size(32, 32));
        return (Icon)icon.Clone();
    }
}
