namespace Ai_DailyTracking.Domain.Models;

public sealed class TrackingProject
{
    public Guid ProjectId { get; set; } = Guid.NewGuid();

    public string ProjectName { get; set; } = string.Empty;

    public string StorageFileName { get; set; } = string.Empty;

    public DateTime CreatedAtLocal { get; set; } = DateTime.Now;

    public DateTime UpdatedAtLocal { get; set; } = DateTime.Now;

    public List<TrackingEntry> Entries { get; set; } = [];

    // Users (email or Windows username) allowed to see this project. Empty means visible to everyone.
    public List<string> AllowedUserNames { get; set; } = [];

    // Extra values typed into EditableOption fields (keyed by field Key) while working on this project only,
    // so a new project starts with just the schema's default options instead of inheriting other projects' values.
    public Dictionary<string, List<string>> CustomFieldOptions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}