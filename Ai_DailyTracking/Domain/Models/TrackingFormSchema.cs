namespace Ai_DailyTracking.Domain.Models;

public sealed class TrackingFormSchema
{
    public string Name { get; set; } = "Tracking";

    public List<TrackingFieldDefinition> Fields { get; set; } = [];
}