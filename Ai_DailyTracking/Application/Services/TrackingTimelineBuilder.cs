using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Ai_DailyTracking.Domain.Models;

namespace Ai_DailyTracking.Application.Services;

public sealed record TrackingTimelineNote(DateTime Timestamp, string Text);

public sealed record TrackingTimelineRow(
    int EntryNumber,
    DateTime Start,
    DateTime End,
    string Status,
    string Summary,
    IReadOnlyList<TrackingTimelineNote> Notes);

public static class TrackingTimelineBuilder
{
    private static readonly Regex NoteStart = new(
        @"^\s*\[(?<date>\d{4}-\d{2}-\d{2} \d{2}:\d{2})\]\s*(?<text>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<TrackingTimelineRow> Build(
        IEnumerable<TrackingEntry> entries,
        TrackingFormSchema schema,
        string dateFieldKey)
    {
        return entries.OrderBy(entry => entry.EntryNumber).ThenBy(entry => entry.EntryId)
            .Select(entry => BuildRow(entry, schema, dateFieldKey)).ToList();
    }

    private static TrackingTimelineRow BuildRow(TrackingEntry entry, TrackingFormSchema schema, string dateFieldKey)
    {
        var notes = ParseNotes(GetValue(entry, "notes"));
        var start = ReadDate(entry, dateFieldKey) ?? entry.CreatedAtLocal.Date;
        var updated = ReadDate(entry, "updatedDate") ?? entry.UpdatedAtLocal.Date;
        var lastNote = notes.Count == 0 ? (DateTime?)null : notes.Max(note => note.Timestamp);
        var end = updated.Date == lastNote?.Date
            ? lastNote.Value
            : updated.Date.AddHours(17);

        if (lastNote is not null && lastNote.Value.Date > end.Date)
            end = lastNote.Value;
        if (end.Date < start.Date)
            end = start.Date.AddHours(17);

        var summary = new StringBuilder($"Registro #{entry.EntryNumber}");
        summary.AppendLine().Append("ID interno: ").Append(entry.EntryId);
        foreach (var field in schema.Fields)
        {
            var value = GetValue(entry, field.Key);
            summary.AppendLine().Append(field.Label).Append(": ")
                .Append(string.IsNullOrWhiteSpace(value) ? "(vacío)" : value);
        }
        var definedKeys = schema.Fields.Select(field => field.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in entry.Values.Where(pair => !definedKeys.Contains(pair.Key)).OrderBy(pair => pair.Key))
            summary.AppendLine().Append(pair.Key).Append(": ")
                .Append(string.IsNullOrWhiteSpace(pair.Value) ? "(vacío)" : pair.Value);

        return new TrackingTimelineRow(entry.EntryNumber, start.Date.AddHours(8), end,
            GetValue(entry, "status") ?? string.Empty, summary.ToString(), notes);
    }

    public static IReadOnlyList<TrackingTimelineNote> ParseNotes(string? rawNotes)
    {
        if (string.IsNullOrWhiteSpace(rawNotes))
            return [];

        var result = new List<TrackingTimelineNote>();
        DateTime? timestamp = null;
        var body = new StringBuilder();

        foreach (var line in rawNotes.Replace("\r\n", "\n").Split('\n'))
        {
            var match = NoteStart.Match(line);
            if (match.Success && DateTime.TryParseExact(match.Groups["date"].Value,
                    "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                if (timestamp is not null)
                    result.Add(new TrackingTimelineNote(timestamp.Value, body.ToString().Trim()));
                timestamp = parsed;
                body.Clear().Append(match.Groups["text"].Value);
            }
            else if (timestamp is not null)
            {
                body.AppendLine().Append(line);
            }
        }

        if (timestamp is not null)
            result.Add(new TrackingTimelineNote(timestamp.Value, body.ToString().Trim()));
        return result.OrderBy(note => note.Timestamp).ToList();
    }

    public static double DayFraction(DateTime timestamp)
    {
        return Math.Clamp((timestamp.TimeOfDay.TotalMinutes - 8 * 60) / (9 * 60), 0, 1);
    }

    private static string? GetValue(TrackingEntry entry, string key) =>
        entry.Values.TryGetValue(key, out var value) ? value : null;

    private static DateTime? ReadDate(TrackingEntry entry, string key) =>
        DateTime.TryParse(GetValue(entry, key), out var date) ? date.Date : null;
}
