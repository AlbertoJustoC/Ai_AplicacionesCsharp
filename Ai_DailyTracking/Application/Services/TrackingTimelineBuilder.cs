using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Ai_DailyTracking.Domain.Models;

namespace Ai_DailyTracking.Application.Services;

// Status is the leading "**Estado**" marker of the note, or null when the note has none.
public sealed record TrackingTimelineNote(DateTime Timestamp, string Text, string? Status = null);

// End is where the solid line stops; ClosedAt, when set, is where a thin dotted line continues to (the "Cerrado" note).
public sealed record TrackingTimelineRow(
    int EntryNumber,
    DateTime Start,
    DateTime End,
    string Status,
    string Summary,
    IReadOnlyList<TrackingTimelineNote> Notes,
    DateTime? ClosedAt = null);

public static class TrackingTimelineBuilder
{
    private const string DoneStatus = "Hecho";
    private const string ClosedStatus = "Cerrado";

    private static readonly Regex NoteStart = new(
        @"^\s*\[(?<date>\d{4}-\d{2}-\d{2} \d{2}:\d{2})\]\s*(?<text>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex LeadingStatus = new(
        @"^\*\*(?<status>[^*\r\n]+)\*\*",
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
        var status = GetValue(entry, "status") ?? string.Empty;
        var (start, end, closedAt) = ResolveSpan(entry, notes, status, dateFieldKey);

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

        return new TrackingTimelineRow(entry.EntryNumber, start, end, status, summary.ToString(), notes, closedAt);
    }

    // First note -> "Hecho" note; a "Cerrado" record adds a dotted span to its closing note; anything else ends at the last change.
    private static (DateTime Start, DateTime End, DateTime? ClosedAt) ResolveSpan(
        TrackingEntry entry,
        IReadOnlyList<TrackingTimelineNote> notes,
        string status,
        string dateFieldKey)
    {
        var recordDate = ReadDate(entry, dateFieldKey) ?? entry.CreatedAtLocal.Date;
        var updated = ReadDate(entry, "updatedDate") ?? entry.UpdatedAtLocal.Date;
        var lastNote = notes.Count == 0 ? (DateTime?)null : notes[^1].Timestamp;

        var start = notes.Count == 0 ? recordDate.AddHours(8) : notes[0].Timestamp;
        var end = updated == lastNote?.Date
            ? lastNote.Value
            : updated.AddHours(17);

        if (lastNote is not null && lastNote.Value.Date > end.Date)
            end = lastNote.Value;
        if (end.Date < start.Date)
            end = start.Date.AddHours(17);

        var statuses = CarryStatusesForward(notes);

        if (statuses.Count > 0 && IsStatus(status, DoneStatus))
        {
            var doneIndex = statuses.FindLastIndex(noteStatus => IsStatus(noteStatus, DoneStatus));
            return doneIndex < 0 ? (start, end, null) : (start, notes[StartOfDoneRun(statuses, doneIndex)].Timestamp, null);
        }

        if (statuses.Count > 0 && IsStatus(status, ClosedStatus) && IsStatus(statuses[^1], ClosedStatus))
        {
            var openIndex = statuses.FindLastIndex(noteStatus => !IsStatus(noteStatus, ClosedStatus));
            var solidIndex = openIndex >= 0 && IsStatus(statuses[openIndex], DoneStatus) ? StartOfDoneRun(statuses, openIndex) : openIndex;
            var solidEnd = solidIndex < 0 ? start : notes[solidIndex].Timestamp;
            return (start, solidEnd, notes[^1].Timestamp > solidEnd ? notes[^1].Timestamp : null);
        }

        return (start, end, null);
    }

    // Index of the note that made the record "Hecho", skipping the later "Hecho" notes that only add comments.
    private static int StartOfDoneRun(List<string?> statuses, int lastDoneIndex)
    {
        var index = lastDoneIndex;

        while (index > 0 && IsStatus(statuses[index - 1], DoneStatus))
            index--;

        return index;
    }

    // A note without its own status marker keeps the status of the previous note.
    private static List<string?> CarryStatusesForward(IReadOnlyList<TrackingTimelineNote> notes)
    {
        var statuses = new List<string?>(notes.Count);
        string? current = null;

        foreach (var note in notes)
        {
            current = note.Status ?? current;
            statuses.Add(current);
        }

        return statuses;
    }

    private static bool IsStatus(string? value, string status) =>
        string.Equals(value?.Trim(), status, StringComparison.OrdinalIgnoreCase);

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
                    result.Add(CreateNote(timestamp.Value, body.ToString()));
                timestamp = parsed;
                body.Clear().Append(match.Groups["text"].Value);
            }
            else if (timestamp is not null)
            {
                body.AppendLine().Append(line);
            }
        }

        if (timestamp is not null)
            result.Add(CreateNote(timestamp.Value, body.ToString()));
        return result.OrderBy(note => note.Timestamp).ToList();
    }

    private static TrackingTimelineNote CreateNote(DateTime timestamp, string body)
    {
        var text = body.Trim();
        var match = LeadingStatus.Match(text);
        return new TrackingTimelineNote(timestamp, text, match.Success ? match.Groups["status"].Value.Trim() : null);
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
