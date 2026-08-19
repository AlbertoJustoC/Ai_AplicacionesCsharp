namespace Ai_DailyTracking.Domain.Models;

// Data needed to render the PDF report: the historical entry rows plus the default chart series.
public sealed class DailyTrackingReportData
{
    public required string ProjectName { get; init; }

    public required DateTime GeneratedAtLocal { get; init; }

    public required IReadOnlyList<DailyTrackingReportRow> Rows { get; init; }

    public required string ChartTitle { get; init; }

    public required IReadOnlyList<DateTime> ChartDates { get; init; }

    public required IReadOnlyList<(string Label, IReadOnlyList<int> Counts)> ChartSeries { get; init; }
}
