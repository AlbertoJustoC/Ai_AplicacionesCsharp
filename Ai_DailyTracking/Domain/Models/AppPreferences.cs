namespace Ai_DailyTracking.Domain.Models;

public sealed class AppPreferences
{
    public Guid? LastProjectId { get; set; }

    // Optional shared folder chosen by the user to store project files; falls back to AppStoragePaths.ProjectsDirectory when null/empty.
    public string? ProjectsFolderPath { get; set; }

    // Most recently selected project folders, newest first.
    public List<string>? RecentProjectsFolderPaths { get; set; }

    // Local sync location for the shared SharePoint document library.
    public string? SharePointLibraryRootPath { get; set; }

    // Last folder used to create/update the per-entry Markdown file.
    public string? LastMarkdownFolderPath { get; set; }
}
