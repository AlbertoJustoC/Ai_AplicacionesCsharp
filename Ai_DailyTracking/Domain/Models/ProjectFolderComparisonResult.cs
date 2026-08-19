namespace Ai_DailyTracking.Domain.Models;

// Result of comparing the projects currently loaded in the app against the ones found in a candidate folder.
public sealed class ProjectFolderComparisonResult
{
    public string CurrentFolderPath { get; init; } = string.Empty;

    public string NewFolderPath { get; init; } = string.Empty;

    // Human-readable lines describing every difference found; used for the history log.
    public List<string> Differences { get; } = new();

    // Same project exists on both sides with different content; the caller must choose which version to keep.
    public List<ProjectFolderConflict> Conflicts { get; } = new();

    // Final set of projects to write to the new folder; conflicts are only added once resolved by the caller.
    public List<TrackingProject> ProjectsToKeep { get; } = new();
}

// A project found on both sides with a different version, pending a user decision on which one to keep.
public sealed class ProjectFolderConflict
{
    public required TrackingProject AppVersion { get; init; }

    public required TrackingProject FolderVersion { get; init; }
}
