namespace Ai_DailyTracking.Domain.Models;

public sealed class TrackingEntry
{
    public Guid EntryId { get; set; } = Guid.NewGuid();

    public int EntryNumber { get; set; }

    public DateTime CreatedAtLocal { get; set; } = DateTime.Now;

    public DateTime UpdatedAtLocal { get; set; } = DateTime.Now;

    public Dictionary<string, string?> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}