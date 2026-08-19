namespace Ai_preAgresso.Shared.Constants;

public static class AppStoragePaths
{
    private static readonly string RootPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AiPreAgresso");

    public static string RootDirectory => RootPath;

    public static string EntriesFile => Path.Combine(RootPath, "entries.json");
}
