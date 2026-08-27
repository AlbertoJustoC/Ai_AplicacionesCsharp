using System.IO;

namespace Ai_NewNotificationBlink.Shared.Constants;

public static class AppStoragePaths
{
    private static readonly string RootFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AiNewNotificationBlink");

    public static string SettingsFile => Path.Combine(RootFolder, "settings.json");

    public static string AutomationTreeDumpFile => Path.Combine(RootFolder, "automation-tree-dump.txt");

    public static string TaskbarTreeDumpFile => Path.Combine(RootFolder, "taskbar-tree-dump.txt");
}
