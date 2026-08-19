using Ai_DailyTracking.Domain.Models;
using Ai_DailyTracking.Shared.Helpers;

namespace Ai_DailyTracking.Application.Services;

// Builds the printable report data (historical rows + default chart series) for a project, with no UI/print dependencies.
public static class DailyTrackingReportBuilder
{
    public static DailyTrackingReportData Build(TrackingProject project, TrackingFormSchema schema)
    {
        var rows = project.Entries
            .OrderByDescending(entry => entry.UpdatedAtLocal)
            .Select(entry => new DailyTrackingReportRow(entry.EntryNumber, GetEntryDate(entry), GetEntryHeadline(entry), GetFieldValue(entry, "status") ?? "-"))
            .ToList();

        var (chartTitle, chartDates, chartSeries) = BuildDefaultChartSeries(project, schema);

        return new DailyTrackingReportData
        {
            ProjectName = project.ProjectName,
            GeneratedAtLocal = DateTime.Now,
            Rows = rows,
            ChartTitle = chartTitle,
            ChartDates = chartDates,
            ChartSeries = chartSeries
        };
    }

    // Mirrors TrackingChartForm's default state: first date field, first option field, every option selected.
    private static (string Title, IReadOnlyList<DateTime> Dates, IReadOnlyList<(string Label, IReadOnlyList<int> Counts)> Series) BuildDefaultChartSeries(TrackingProject project, TrackingFormSchema schema)
    {
        var dateField = schema.Fields.FirstOrDefault(field => field.Type == TrackingFieldType.Date);
        var seriesField = schema.Fields.FirstOrDefault(field => field.Type is TrackingFieldType.Option or TrackingFieldType.EditableOption);

        if (dateField is null || seriesField is null)
        {
            return ("No hay campos suficientes en el esquema para generar el grafico.", [], []);
        }

        var seriesOptions = FieldOptionsHelper.GetOptions(project, seriesField);

        if (seriesOptions.Count == 0)
        {
            return ("No hay campos suficientes en el esquema para generar el grafico.", [], []);
        }

        var datedEntries = project.Entries
            .Select(entry => (Entry: entry, Date: TryGetDate(entry, dateField)))
            .Where(item => item.Date is not null)
            .Select(item => (item.Entry, Date: item.Date!.Value))
            .ToList();

        var dates = datedEntries.Select(item => item.Date).Distinct().OrderBy(date => date).ToList();

        var series = seriesOptions
            .Select(optionValue =>
            {
                var counts = dates
                    .Select(date => datedEntries.Count(item => item.Date == date && string.Equals(GetFieldValue(item.Entry, seriesField.Key), optionValue, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                return (Label: optionValue, Counts: (IReadOnlyList<int>)counts);
            })
            .ToList();

        return ($"{seriesField.Label}: registros por dia ({datedEntries.Count} registro(s))", dates, series);
    }

    private static DateTime? TryGetDate(TrackingEntry entry, TrackingFieldDefinition dateField)
    {
        return entry.Values.TryGetValue(dateField.Key, out var rawDate) && DateTime.TryParse(rawDate, out var parsedDate) ? parsedDate.Date : null;
    }

    private static string GetEntryDate(TrackingEntry entry)
    {
        var storedDate = GetFieldValue(entry, "recordDate", "date", "fecha");
        return DateTime.TryParse(storedDate, out var parsedDate) ? parsedDate.ToString("dd/MM/yyyy") : entry.UpdatedAtLocal.ToString("dd/MM/yyyy");
    }

    private static string GetEntryHeadline(TrackingEntry entry)
    {
        return GetFieldValue(entry, "activity", "descripcion", "title") ?? GetFieldValue(entry, "area", "package") ?? "Registro sin titulo";
    }

    private static string? GetFieldValue(TrackingEntry entry, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (entry.Values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
