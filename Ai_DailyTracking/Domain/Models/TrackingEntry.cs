namespace Ai_DailyTracking.Domain.Models;

public sealed class TrackingEntry
{
    public Guid EntryId { get; set; } = Guid.NewGuid();

    public int EntryNumber { get; set; }

    public DateTime CreatedAtLocal { get; set; } = DateTime.Now;

    // Present only for entries created after the shared change-log migration.
    public DateTimeOffset? CreatedAtUtc { get; set; }

    public int? LegacyOrder { get; set; }

    public DateTime UpdatedAtLocal { get; set; } = DateTime.Now;

    private Dictionary<string, DateTimeOffset> _fieldModifiedAtUtc = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, DateTimeOffset> FieldModifiedAtUtc
    {
        get => _fieldModifiedAtUtc;
        set => _fieldModifiedAtUtc = new Dictionary<string, DateTimeOffset>(value ?? [], StringComparer.OrdinalIgnoreCase);
    }

    private Dictionary<string, string?> _values = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string?> Values
    {
        get => _values;
        set => _values = new Dictionary<string, string?>(value ?? [], StringComparer.OrdinalIgnoreCase);
    }
}
