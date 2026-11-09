using Microsoft.Win32;

namespace DIMPIK.Services;

public static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static string LauncherPath => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "DIMPIK.exe"));
    public static bool Available => File.Exists(LauncherPath);
    public static bool Enabled { get { using var key = Registry.CurrentUser.OpenSubKey(RunKey); return key?.GetValue("DIMPIK") is string || key?.GetValue("ASnapdo") is string; } }
    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
        {
            if (!Available) throw new InvalidOperationException("설치형 버전에서 자동 실행을 설정하세요.");
            key.SetValue("DIMPIK", $"\"{LauncherPath}\" --background");
            key.DeleteValue("ASnapdo", false);
        }
        else { key.DeleteValue("DIMPIK", false); key.DeleteValue("ASnapdo", false); }
    }
}
