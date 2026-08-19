namespace Ai_DailyTracking.Shared.Constants;

public static class AppStoragePaths
{
    private static readonly string RootPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AiDailyTracking");

    public static string RootDirectory => RootPath;

    public static string ProjectsDirectory => Path.Combine(RootPath, "Projects");

    public static string BackupDirectory => Path.Combine(RootPath, "backup");

    public static string PreferencesFile => Path.Combine(RootPath, "app-preferences.json");

    public static string SchemaFile => Path.Combine(RootPath, "tracking-schema.json");
}