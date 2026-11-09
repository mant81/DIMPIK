using System.Globalization;

namespace DIMPIK.Services;

internal static class CaptureNames
{
    private const string FileTimeFormat = "yyyy-MM-dd_HH-mm-ss";

    public static string Create(DateTime time, IEnumerable<string> existingNames)
    {
        var names = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var labels = names.Select(TabTitle).ToHashSet(StringComparer.OrdinalIgnoreCase);
        string prefix = time.ToString(FileTimeFormat, CultureInfo.InvariantCulture);
        for (int number = 1; ; number++)
        {
            string name = prefix + (number == 1 ? "" : $" ({number})") + ".png";
            if (!names.Contains(name) && !labels.Contains(TabTitle(name))) return name;
        }
    }

    public static string TabTitle(string name)
    {
        if (name.Length < 23 || !name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
            !DateTime.TryParseExact(name[..19], FileTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var captured))
            return name;
        string suffix = name[19..^4];
        if (suffix.Length > 0 && (!suffix.StartsWith(" (") || !suffix.EndsWith(')') || !int.TryParse(suffix[2..^1], out _))) return name;
        return captured.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + suffix;
    }
}
