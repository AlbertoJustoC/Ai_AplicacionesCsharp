namespace Ai_DailyTracking.Domain.Models;

public sealed class ProjectChangeBatch
{
    public Guid BatchId { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string BaselineHash { get; set; } = string.Empty;
    public bool ProjectDeleted { get; set; }
    public List<ProjectChange> Changes { get; set; } = [];
}

public sealed class ProjectChange
{
    public Guid ChangeId { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid EntryId { get; set; }
    public DateTimeOffset SavedAtUtc { get; set; }
    public DateTimeOffset? CreatedAtUtc { get; set; }
    public string Author { get; set; } = string.Empty;
    public string Machine { get; set; } = string.Empty;
    public List<Guid> ObservedEntryChanges { get; set; } = [];
    public List<ProjectFieldChange> Fields { get; set; } = [];
    public List<ProjectNoteLine> Notes { get; set; } = [];
}

public sealed class ProjectFieldChange
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public List<Guid> Supersedes { get; set; } = [];
}

public sealed class ProjectNoteLine
{
    public Guid NoteId { get; set; } = Guid.NewGuid();
    public int Sequence { get; set; }
    public DateTimeOffset SavedAtUtc { get; set; }
    public string Author { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public sealed record ProjectFieldCandidate(Guid ChangeId, string? Value, string Author, string Machine, DateTimeOffset SavedAtUtc);

public sealed record ProjectFieldConflict(Guid EntryId, int EntryNumber, string FieldKey,
    IReadOnlyList<ProjectFieldCandidate> Candidates);

public sealed record ProjectMergeResult(TrackingProject Project, IReadOnlyList<ProjectFieldConflict> Conflicts,
    int IncorporatedChanges);
