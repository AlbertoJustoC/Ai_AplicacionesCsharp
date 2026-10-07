namespace Ai_DailyTracking.Domain.Models;

public sealed class ProjectPresence
{
    public Guid SessionId { get; set; }
    public Guid ProjectId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
