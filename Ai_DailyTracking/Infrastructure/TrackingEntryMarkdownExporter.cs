using Ai_DailyTracking.Domain.Models;
using Ai_DailyTracking.Shared.Helpers;
using System.Text;

namespace Ai_DailyTracking.Infrastructure;

public sealed class TrackingEntryMarkdownExporter
{
    private const string ActivitySectionHeader = "## Actividad";
    private const string NotesSectionHeader = "## Notas";
    private const string NotesFieldKey = "notes";

    private static readonly (string PropertyName, string FieldKey)[] FrontmatterMapping =
    [
        ("Disciplina", "discipline"),
        ("Area", "area"),
        ("Paquete", "package"),
        ("Solicitado", "createdBy"),
        ("Canal", "channel"),
        ("tipo", "type"),
        ("Fecha", "recordDate"),
        ("Prioridad", "priority"),
        ("Complejidad", "complexity"),
        ("Estado", "status"),
        ("Cambio Estado", "updatedDate")
    ];

    private readonly NotesSyncStateStore? _syncStateStore;
    // Bases computed when pulling the markdown into a ficha. They only become the stored base once the ficha
    // has been saved (CommitPendingNotesBases); otherwise a discarded ficha would disagree with its own base.
    private readonly Dictionary<string, PendingBase> _pendingBases = new(StringComparer.OrdinalIgnoreCase);

    // Without a state store there is no record of the last synchronized notes, so every sync behaves like the
    // first one: notes only present on one side are added to the other and nothing is deleted.
    public TrackingEntryMarkdownExporter(NotesSyncStateStore? syncStateStore = null)
    {
        _syncStateStore = syncStateStore;
    }

    public void CommitPendingNotesBases()
    {
        foreach (var pending in _pendingBases.Values)
        {
            _syncStateStore?.Save(pending.EntryId, pending.MarkdownFilePath, pending.Lines);
        }

        _pendingBases.Clear();
    }

    public void DiscardPendingNotesBases()
    {
        _pendingBases.Clear();
    }

    public bool TryCreateOrUpdate(string markdownFilePath, string templatePath, TrackingEntry entry, out string synchronizedNotesText, out string? errorMessage)
    {
        return TryCreateOrUpdate(markdownFilePath, templatePath, entry, null, out synchronizedNotesText, out errorMessage);
    }

    // notesModifiedAtUtc is when the ficha notes were last edited; it decides who wins when the ficha and the
    // Markdown changed the same note since the last synchronization (the most recent update prevails).
    public bool TryCreateOrUpdate(string markdownFilePath, string templatePath, TrackingEntry entry, DateTimeOffset? notesModifiedAtUtc, out string synchronizedNotesText, out string? errorMessage)
    {
        errorMessage = null;
        synchronizedNotesText = string.Empty;

        try
        {
            var frontmatterValues = BuildFrontmatterValues(entry);
            var incomingNotes = ReadEntryValue(entry, NotesFieldKey);
            string markdownContent;

            if (File.Exists(markdownFilePath))
            {
                var existingContent = File.ReadAllText(markdownFilePath);
                var frontmatterUpdated = UpdateFrontmatter(existingContent, frontmatterValues);
                var markdownWins = IsMarkdownMoreRecent(markdownFilePath, entry, notesModifiedAtUtc);
                markdownContent = SyncNotesSection(frontmatterUpdated, incomingNotes, entry.EntryId, markdownFilePath, markdownWins, out synchronizedNotesText);
            }
            else
            {
                if (!File.Exists(templatePath))
                {
                    errorMessage = "No se encontro la plantilla de Markdown (Tareas - copia.md).";
                    return false;
                }

                var templateContent = File.ReadAllText(templatePath);
                markdownContent = CreateFromTemplate(templateContent, frontmatterValues, ReadEntryValue(entry, "activity"), incomingNotes);
                synchronizedNotesText = NormalizeForEditor(SortNotesLinesByTimestamp(incomingNotes));
            }

            var directoryPath = Path.GetDirectoryName(markdownFilePath);

            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            File.WriteAllText(markdownFilePath, markdownContent, new UTF8Encoding(false));

            // Ficha and markdown now hold exactly the same notes.
            _pendingBases.Remove(PendingKey(entry.EntryId, markdownFilePath));
            _syncStateStore?.Save(entry.EntryId, markdownFilePath, NoteLinesMerge.ToLines(synchronizedNotesText));
            return true;
        }
        catch (Exception exception)
        {
            synchronizedNotesText = NormalizeForEditor(ReadEntryValue(entry, NotesFieldKey));
            errorMessage = exception.Message;
            return false;
        }
    }

    // Reads "## Notas" from an existing Markdown file and merges what changed there (edited/deleted/new notes)
    // into the ficha notes without writing the file. Returns true only when the ficha notes must change.
    public bool TryPullNotes(string markdownFilePath, TrackingEntry entry, DateTimeOffset? notesModifiedAtUtc, out string mergedNotesText)
    {
        mergedNotesText = string.Empty;

        try
        {
            if (!File.Exists(markdownFilePath))
            {
                return false;
            }

            var notesBody = ReadSectionBody(File.ReadAllText(markdownFilePath), NotesSectionHeader, null);

            if (notesBody is null)
            {
                return false;
            }

            var fichaLines = NoteLinesMerge.ToLines(ReadEntryValue(entry, NotesFieldKey));
            var markdownLines = NoteLinesMerge.ToLines(notesBody);
            var baseLines = ResolveBaseLines(entry.EntryId, markdownFilePath, fichaLines, markdownLines);
            var markdownWins = IsMarkdownMoreRecent(markdownFilePath, entry, notesModifiedAtUtc);
            var result = MergeNoteLines(baseLines, fichaLines, markdownLines, markdownWins);

            if (_syncStateStore is not null)
            {
                _pendingBases[PendingKey(entry.EntryId, markdownFilePath)] = new PendingBase(entry.EntryId, markdownFilePath, result.SyncedLines);
            }

            if (NoteLinesMerge.HaveSameLines(result.Lines, fichaLines))
            {
                return false;
            }

            mergedNotesText = NormalizeForEditor(SortNotesLinesByTimestamp(string.Join("\n", result.Lines)));
            return true;
        }
        catch (Exception)
        {
            // A locked or unreadable file must never block opening the ficha; the next pull retries.
            return false;
        }
    }

    private IReadOnlyList<string> ResolveBaseLines(Guid entryId, string markdownFilePath, IReadOnlyList<string> fichaLines, IReadOnlyList<string> markdownLines)
    {
        if (_pendingBases.TryGetValue(PendingKey(entryId, markdownFilePath), out var pending))
        {
            return pending.Lines;
        }

        var storedLines = _syncStateStore?.TryGet(entryId, markdownFilePath);

        if (storedLines is not null)
        {
            return storedLines;
        }

        // No record of a previous sync: only the notes both sides already share are known to be in sync, so a
        // note present on one side only is treated as new there (never as deleted on the other side).
        return NoteLinesMerge.Intersect(markdownLines, fichaLines);
    }

    private static NoteLinesMergeResult MergeNoteLines(IReadOnlyList<string> baseLines, IReadOnlyList<string> fichaLines, IReadOnlyList<string> markdownLines, bool markdownWins)
    {
        // An empty "## Notas" is read as "not written yet", not as "the user deleted every note".
        var effectiveMarkdownLines = markdownLines.Count == 0 && baseLines.Count > 0 ? baseLines : markdownLines;
        return NoteLinesMerge.Merge(baseLines, fichaLines, effectiveMarkdownLines, markdownWins);
    }

    private static string PendingKey(Guid entryId, string markdownFilePath)
    {
        return entryId.ToString("N") + "|" + Path.GetFullPath(markdownFilePath);
    }

    private sealed record PendingBase(Guid EntryId, string MarkdownFilePath, IReadOnlyList<string> Lines);

    private static bool IsMarkdownMoreRecent(string markdownFilePath, TrackingEntry entry, DateTimeOffset? notesModifiedAtUtc)
    {
        var fichaModifiedAtUtc = notesModifiedAtUtc
            ?? (entry.FieldModifiedAtUtc.TryGetValue(NotesFieldKey, out var storedValue) ? storedValue : DateTimeOffset.MinValue);
        var markdownModifiedAtUtc = new DateTimeOffset(File.GetLastWriteTimeUtc(markdownFilePath), TimeSpan.Zero);
        return markdownModifiedAtUtc > fichaModifiedAtUtc;
    }

    private static Dictionary<string, string> BuildFrontmatterValues(TrackingEntry entry)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mapping in FrontmatterMapping)
        {
            values[mapping.PropertyName] = ReadEntryValue(entry, mapping.FieldKey);
        }

        return values;
    }

    private static string ReadEntryValue(TrackingEntry entry, string fieldKey)
    {
        return entry.Values.TryGetValue(fieldKey, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : string.Empty;
    }

    private static string CreateFromTemplate(string templateContent, IReadOnlyDictionary<string, string> values, string activityText, string notesText)
    {
        var frontmatterApplied = UpdateFrontmatter(templateContent, values);
        var activityApplied = ReplaceSectionBody(frontmatterApplied, ActivitySectionHeader, NotesSectionHeader, activityText);
        var orderedNotesText = SortNotesLinesByTimestamp(notesText);
        return ReplaceSectionBody(activityApplied, NotesSectionHeader, null, orderedNotesText);
    }

    private static string UpdateFrontmatter(string markdownContent, IReadOnlyDictionary<string, string> values)
    {
        var newline = DetectLineEnding(markdownContent);
        var normalized = NormalizeLineEndings(markdownContent);
        var lines = normalized.Split('\n').ToList();

        if (lines.Count > 1 && string.Equals(lines[0].Trim(), "---", StringComparison.Ordinal))
        {
            var endIndex = -1;

            for (var i = 1; i < lines.Count; i++)
            {
                if (string.Equals(lines[i].Trim(), "---", StringComparison.Ordinal))
                {
                    endIndex = i;
                    break;
                }
            }

            if (endIndex > 0)
            {
                var currentFrontmatterLines = lines.Skip(1).Take(endIndex - 1).ToList();
                var updatedFrontmatterLines = UpdateFrontmatterLines(currentFrontmatterLines, values);
                var bodyLines = lines.Skip(endIndex + 1);

                var rebuiltLines = new List<string> { "---" };
                rebuiltLines.AddRange(updatedFrontmatterLines);
                rebuiltLines.Add("---");
                rebuiltLines.AddRange(bodyLines);

                return ApplyLineEnding(string.Join("\n", rebuiltLines), newline);
            }
        }

        var rebuiltContent = BuildFrontmatterBlock(values, "\n") + normalized.TrimStart('\n');
        return ApplyLineEnding(rebuiltContent, newline);
    }

    private static List<string> UpdateFrontmatterLines(IReadOnlyList<string> currentLines, IReadOnlyDictionary<string, string> values)
    {
        var updatedLines = new List<string>(currentLines.Count + FrontmatterMapping.Length);
        var applied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in currentLines)
        {
            var separatorIndex = line.IndexOf(':');

            if (separatorIndex <= 0)
            {
                updatedLines.Add(line);
                continue;
            }

            var key = line[..separatorIndex].Trim();

            if (!values.TryGetValue(key, out var value))
            {
                updatedLines.Add(line);
                continue;
            }

            updatedLines.Add($"{key}: {value}");
            applied.Add(key);
        }

        foreach (var mapping in FrontmatterMapping)
        {
            if (applied.Contains(mapping.PropertyName))
            {
                continue;
            }

            if (!values.TryGetValue(mapping.PropertyName, out var value))
            {
                value = string.Empty;
            }

            updatedLines.Add($"{mapping.PropertyName}: {value}");
        }

        return updatedLines;
    }

    private static string BuildFrontmatterBlock(IReadOnlyDictionary<string, string> values, string newline)
    {
        var builder = new StringBuilder();
        builder.Append("---").Append(newline);

        foreach (var mapping in FrontmatterMapping)
        {
            values.TryGetValue(mapping.PropertyName, out var value);
            builder.Append(mapping.PropertyName).Append(": ").Append(value ?? string.Empty).Append(newline);
        }

        builder.Append("---").Append(newline);
        return builder.ToString();
    }

    private string SyncNotesSection(string markdownContent, string incomingNotesText, Guid entryId, string markdownFilePath, bool markdownWins, out string synchronizedNotesText)
    {
        var normalizedIncoming = NormalizeLineEndings(incomingNotesText).Trim('\n', '\r');

        var existingNotesBody = ReadSectionBody(markdownContent, NotesSectionHeader, null);

        if (existingNotesBody is null)
        {
            var orderedIncoming = SortNotesLinesByTimestamp(normalizedIncoming);
            synchronizedNotesText = NormalizeForEditor(orderedIncoming);

            if (orderedIncoming.Length == 0)
            {
                return markdownContent;
            }

            return ReplaceSectionBody(markdownContent, NotesSectionHeader, null, orderedIncoming);
        }

        var fichaLines = NoteLinesMerge.ToLines(normalizedIncoming);
        var markdownLines = NoteLinesMerge.ToLines(existingNotesBody);
        var baseLines = ResolveBaseLines(entryId, markdownFilePath, fichaLines, markdownLines);
        var mergedLines = MergeNoteLines(baseLines, fichaLines, markdownLines, markdownWins).Lines;
        var orderedMergedBody = SortNotesLinesByTimestamp(string.Join("\n", mergedLines));
        synchronizedNotesText = NormalizeForEditor(orderedMergedBody);
        return ReplaceSectionBody(markdownContent, NotesSectionHeader, null, orderedMergedBody);
    }

    private static string NormalizeForEditor(string notesText)
    {
        var normalized = NormalizeLineEndings(notesText ?? string.Empty).Trim('\n', '\r');
        return normalized.Replace("\n", Environment.NewLine, StringComparison.Ordinal);
    }

    private static string? ReadSectionBody(string markdownContent, string sectionHeader, string? nextSectionHeader)
    {
        var normalized = NormalizeLineEndings(markdownContent);
        var sectionHeaderIndex = FindHeaderLineIndex(normalized, sectionHeader, 0);

        if (sectionHeaderIndex < 0)
        {
            return null;
        }

        var headerLineEnd = normalized.IndexOf('\n', sectionHeaderIndex);

        if (headerLineEnd < 0)
        {
            return string.Empty;
        }

        var bodyStart = headerLineEnd + 1;
        var bodyEnd = nextSectionHeader is null
            ? normalized.Length
            : FindHeaderLineIndex(normalized, nextSectionHeader, bodyStart);

        if (bodyEnd < 0)
        {
            bodyEnd = normalized.Length;
        }

        return normalized[bodyStart..bodyEnd].Trim('\n', '\r');
    }

    private static string SortNotesLinesByTimestamp(string notesBody)
    {
        var lines = NormalizeLineEndings(notesBody ?? string.Empty)
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (lines.Count <= 1)
        {
            return lines.Count == 0 ? string.Empty : lines[0];
        }

        var indexedLines = lines
            .Select((line, index) => new
            {
                Line = line,
                Index = index,
                HasTimestamp = TryParseBracketTimestamp(line, out var timestamp),
                Timestamp = timestamp
            })
            .ToList();

        var ordered = indexedLines
            .Where(item => item.HasTimestamp)
            .OrderBy(item => item.Timestamp)
            .ThenBy(item => item.Index)
            .Select(item => item.Line)
            .Concat(indexedLines.Where(item => !item.HasTimestamp).OrderBy(item => item.Index).Select(item => item.Line));

        return string.Join("\n", ordered);
    }

    private static bool TryParseBracketTimestamp(string textLine, out DateTime timestamp)
    {
        timestamp = default;
        var text = ExtractBracketTimestamp(textLine);

        if (string.IsNullOrWhiteSpace(text) || text.Length != 16)
        {
            return false;
        }

        if (!int.TryParse(text.AsSpan(0, 4), out var year) ||
            !int.TryParse(text.AsSpan(5, 2), out var month) ||
            !int.TryParse(text.AsSpan(8, 2), out var day) ||
            !int.TryParse(text.AsSpan(11, 2), out var hour) ||
            !int.TryParse(text.AsSpan(14, 2), out var minute))
        {
            return false;
        }

        if (text[4] != '-' || text[7] != '-' || text[10] != ' ' || text[13] != ':')
        {
            return false;
        }

        try
        {
            timestamp = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Local);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? ExtractBracketTimestamp(string textLine)
    {
        var line = textLine.TrimStart();

        if (!line.StartsWith("[", StringComparison.Ordinal))
        {
            return null;
        }

        var closingIndex = line.IndexOf(']');

        if (closingIndex <= 1)
        {
            return null;
        }

        var timestamp = line[1..closingIndex].Trim();
        return timestamp.Length == 0 ? null : timestamp;
    }

    private static string ReplaceSectionBody(string markdownContent, string sectionHeader, string? nextSectionHeader, string sectionBody)
    {
        var newline = DetectLineEnding(markdownContent);
        var normalized = NormalizeLineEndings(markdownContent);

        var sectionHeaderIndex = FindHeaderLineIndex(normalized, sectionHeader, 0);

        if (sectionHeaderIndex < 0)
        {
            var appended = normalized.TrimEnd('\n') + "\n" + sectionHeader + "\n" + BuildSectionBody(sectionBody);
            return ApplyLineEnding(appended, newline);
        }

        var headerLineEnd = normalized.IndexOf('\n', sectionHeaderIndex);

        if (headerLineEnd < 0)
        {
            headerLineEnd = normalized.Length;
            normalized += "\n";
        }

        var bodyStart = headerLineEnd + 1;
        var bodyEnd = nextSectionHeader is null
            ? normalized.Length
            : FindHeaderLineIndex(normalized, nextSectionHeader, bodyStart);

        if (bodyEnd < 0)
        {
            bodyEnd = normalized.Length;
        }

        var updated = normalized[..bodyStart] + BuildSectionBody(sectionBody) + normalized[bodyEnd..];
        return ApplyLineEnding(updated, newline);
    }

    private static int FindHeaderLineIndex(string normalizedContent, string header, int searchStart)
    {
        var target = NormalizeLineEndings(header);
        var index = searchStart;

        while (index < normalizedContent.Length)
        {
            var lineEnd = normalizedContent.IndexOf('\n', index);
            var endExclusive = lineEnd >= 0 ? lineEnd : normalizedContent.Length;
            var line = normalizedContent[index..endExclusive].TrimEnd('\r');

            if (string.Equals(line, target, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }

            if (lineEnd < 0)
            {
                break;
            }

            index = lineEnd + 1;
        }

        return -1;
    }

    private static string BuildSectionBody(string sectionBody)
    {
        var normalizedBody = NormalizeLineEndings(sectionBody ?? string.Empty).TrimEnd('\n');

        if (normalizedBody.Length == 0)
        {
            return "\n\n";
        }

        return normalizedBody + "\n\n";
    }

    private static string NormalizeLineEndings(string text)
    {
        return text.Replace("\r\n", "\n");
    }

    private static string DetectLineEnding(string text)
    {
        return text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
    }

    private static string ApplyLineEnding(string normalizedText, string newline)
    {
        return string.Equals(newline, "\n", StringComparison.Ordinal)
            ? normalizedText
            : normalizedText.Replace("\n", "\r\n", StringComparison.Ordinal);
    }
}
