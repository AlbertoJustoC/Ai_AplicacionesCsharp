namespace Ai_DailyTracking.Domain.Models;

public sealed class AppPreferences
{
    public Guid? LastProjectId { get; set; }

    // Optional shared folder chosen by the user to store project files; falls back to AppStoragePaths.ProjectsDirectory when null/empty.
    public string? ProjectsFolderPath { get; set; }
}