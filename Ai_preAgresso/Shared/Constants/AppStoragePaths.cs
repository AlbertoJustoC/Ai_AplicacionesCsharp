namespace Ai_preAgresso.Shared.Constants;

public static class AppStoragePaths
{
    private static readonly string RootPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AiPreAgresso");

    public static string RootDirectory => RootPath;

    // Default location used only until the user picks (or previously picked) a project file elsewhere.
    public static string EntriesFile => Path.Combine(RootPath, "entries.json");

    public static string PreferencesFile => Path.Combine(RootPath, "preferences.json");
}
