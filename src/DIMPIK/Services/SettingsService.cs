using System.Text.Json;

namespace DIMPIK.Services;

public sealed class AppSettings
{
    public Keys RegionHotkey { get; set; } = Keys.PrintScreen;
    public Keys FullscreenHotkey { get; set; } = Keys.Control | Keys.Shift | Keys.D1;
    public bool AutoCopy { get; set; } = true;
    public bool NewCaptureSelectRegion { get; set; } = true;
    public bool ShowGallery { get; set; }
    public bool ShowRulers { get; set; }
    public bool ShowGrid { get; set; }
    public bool ShowStatusBar { get; set; } = true;
    public bool ShowToolbar { get; set; } = true;
    public string ToolbarPosition { get; set; } = "Top";
    public bool AutoSave { get; set; }
    public static string DefaultAutoSaveFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "DIMPIK");
    public string AutoSaveFolder { get; set; } = DefaultAutoSaveFolder;
}

public static class SettingsService
{
    public static string Folder => Environment.GetEnvironmentVariable("DIMPIK_DATA_DIR") is { Length: > 0 } current
        ? current
        : Environment.GetEnvironmentVariable("ASNAPDO_DATA_DIR") is { Length: > 0 } legacyOverride
            ? legacyOverride
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DIMPIK");
    public static AppSettings Load()
    {
        Directory.CreateDirectory(Folder);
        MigrateLegacyFiles();
        var path = Path.Combine(Folder, "settings.json");
        if (!File.Exists(path)) return new();
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > 1024 * 1024) throw new InvalidDataException("설정 파일이 1MiB 제한을 초과합니다.");
            return JsonSerializer.Deserialize<AppSettings>(stream) ?? new();
        }
        catch (JsonException) { File.Copy(path, path + $".invalid-{DateTime.Now:yyyyMMddHHmmssfff}", false); return new(); }
    }
    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Folder);
        var path = Path.Combine(Folder, "settings.json");
        if (File.Exists(path)) File.Copy(path, path + ".bak", true);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, path, true);
    }

    private static bool IsLegacyAutoSaveFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return false;
        try
        {
            string oldDefault = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ASnapdo")));
            string configured = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
            return string.Equals(configured, oldDefault, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }

    private static void MigrateLegacyFiles()
    {
        string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string currentTarget = Path.GetFullPath(Folder);
        string[] oldFolders = [Path.Combine(localData, "ASnapdo"), Path.Combine(localData, "ACapdo")];
        foreach (string name in new[] { "settings.json", "session.json" })
        {
            string destination = Path.Combine(currentTarget, name);
            if (File.Exists(destination)) continue;
            foreach (string oldFolder in oldFolders)
            {
                if (string.Equals(Path.GetFullPath(oldFolder), currentTarget, StringComparison.OrdinalIgnoreCase)) continue;
                string source = Path.Combine(oldFolder, name);
                if (!File.Exists(source)) continue;
                long limit = name == "settings.json" ? 1024 * 1024 : 129L * 1024 * 1024;
                var info = new FileInfo(source);
                if (info.Length > limit) throw new InvalidDataException($"이전 {name} 파일이 {limit / (1024 * 1024)}MiB 제한을 초과합니다.");
                File.Copy(source, destination, false);
                break;
            }
        }
    }
}
