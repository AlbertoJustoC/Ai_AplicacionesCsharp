using Ai_DailyTracking.Domain.Models;

namespace Ai_DailyTracking.Application.Services;

// Shared cumulative line-chart series computation used by TrackingChartForm and the "Crear informe" PDF export.
public static class TrackingChartSeriesBuilder
{
    // Sentinel option representing entries where the series field has no value.
    public const string EmptyValueOption = "(Vacio)";

    public static string GetDisplayValue(TrackingEntry entry, string fieldKey)
    {
        return entry.Values.TryGetValue(fieldKey, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : EmptyValueOption;
    }

    // Builds one cumulative (running-total) line per selected option with matching records, plus an always-present "Total" line.
    public static (IReadOnlyList<DateTime> Dates, IReadOnlyList<(string Label, IReadOnlyList<int> Counts)> Series) Build(
        IReadOnlyList<TrackingEntry> entries,
        TrackingFieldDefinition? dateField,
        TrackingFieldDefinition seriesField,
        IReadOnlyList<string> selectedOptionValues)
    {
        if (dateField is null)
        {
            return ([], []);
        }

        var datedEntries = entries
            .Select(entry => (Entry: entry, Date: TryGetEntryDate(entry, dateField)))
            .Where(item => item.Date is not null)
            .Select(item => (item.Entry, Date: item.Date!.Value))
            .ToList();

        var dates = datedEntries.Select(item => item.Date).Distinct().OrderBy(date => date).ToList();

        var series = selectedOptionValues
            .Select(optionValue =>
            {
                var runningTotal = 0;
                var counts = dates
                    .Select(date =>
                    {
                        runningTotal += datedEntries.Count(item => item.Date == date && string.Equals(GetDisplayValue(item.Entry, seriesField.Key), optionValue, StringComparison.OrdinalIgnoreCase));
                        return runningTotal;
                    })
                    .ToList();
                return (Label: optionValue, Counts: (IReadOnlyList<int>)counts);
            })
            // Hide options with no matching records from the legend/chart.
            .Where(item => item.Counts.Count > 0 && item.Counts[^1] > 0)
            .ToList();

        // Extra line with the sum of the other series for each day.
        var totalCounts = dates.Select((_, dateIndex) => series.Sum(item => item.Counts[dateIndex])).ToList();
        series.Add((Label: "Total", Counts: totalCounts));

        return (dates, series);
    }

    private static DateTime? TryGetEntryDate(TrackingEntry entry, TrackingFieldDefinition dateField)
    {
        return entry.Values.TryGetValue(dateField.Key, out var rawDate) && DateTime.TryParse(rawDate, out var parsedDate) ? parsedDate.Date : null;
    }
}
