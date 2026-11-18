namespace Ai_DailyTracking.Shared.Helpers;

// BaseIndex/NewIndex both set = the line was edited; only BaseIndex = it was deleted; only NewIndex = it was added.
public readonly record struct NoteLineEdit(int? BaseIndex, int? NewIndex);

// Lines is the merged text. SyncedLines is what both sides are known to agree on after adopting the markdown's
// changes (the common ancestor for the next merge); changes still pending on the ficha side are not in it.
public readonly record struct NoteLinesMergeResult(IReadOnlyList<string> Lines, IReadOnlyList<string> SyncedLines);

// Line-based diff and three-way merge of the "notes" text (one note per line) used to keep the ficha, the
// project JSON and the Markdown "## Notas" section in sync in both directions.
public static class NoteLinesMerge
{
    public static IReadOnlyList<string> ToLines(string? text)
    {
        return (text ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
    }

    // Lines are matched by text regardless of their position (so re-sorting a note list is not a change). The
    // unmatched lines are then paired by "gap" (how many matched lines precede them) so a line edited in place
    // is reported as an edit instead of a delete plus an unrelated add.
    public static IReadOnlyList<NoteLineEdit> Diff(IReadOnlyList<string> baseLines, IReadOnlyList<string> newLines)
    {
        var available = new Dictionary<string, Queue<int>>(StringComparer.Ordinal);
        for (var index = 0; index < baseLines.Count; index++)
        {
            if (!available.TryGetValue(baseLines[index], out var queue))
            {
                available[baseLines[index]] = queue = new Queue<int>();
            }

            queue.Enqueue(index);
        }

        var baseMatched = new bool[baseLines.Count];
        var newMatched = new bool[newLines.Count];
        for (var index = 0; index < newLines.Count; index++)
        {
            if (available.TryGetValue(newLines[index], out var queue) && queue.Count > 0)
            {
                baseMatched[queue.Dequeue()] = true;
                newMatched[index] = true;
            }
        }

        var removedByGap = UnmatchedByGap(baseMatched);
        var addedByGap = UnmatchedByGap(newMatched);
        var edits = new List<NoteLineEdit>();

        foreach (var gap in removedByGap.Keys.Union(addedByGap.Keys).OrderBy(value => value))
        {
            removedByGap.TryGetValue(gap, out var removed);
            addedByGap.TryGetValue(gap, out var added);
            var removedCount = removed?.Count ?? 0;
            var addedCount = added?.Count ?? 0;

            for (var position = 0; position < Math.Max(removedCount, addedCount); position++)
            {
                edits.Add(new NoteLineEdit(
                    position < removedCount ? removed![position] : null,
                    position < addedCount ? added![position] : null));
            }
        }

        return edits;
    }

    // Lines present on both sides (as a multiset), in the order of the first list.
    public static IReadOnlyList<string> Intersect(IReadOnlyList<string> first, IReadOnlyList<string> second)
    {
        var remaining = CountLines(second);
        var common = new List<string>();

        foreach (var line in first)
        {
            if (remaining.TryGetValue(line, out var count) && count > 0)
            {
                remaining[line] = count - 1;
                common.Add(line);
            }
        }

        return common;
    }

    public static bool HaveSameLines(IReadOnlyList<string> first, IReadOnlyList<string> second)
    {
        return first.Count == second.Count && Diff(first, second).Count == 0;
    }

    // Applies the edits each side made since baseLines. When both sides changed the same base line differently,
    // the side with the most recent update wins (markdownWins).
    public static NoteLinesMergeResult Merge(
        IReadOnlyList<string> baseLines,
        IReadOnlyList<string> fichaLines,
        IReadOnlyList<string> markdownLines,
        bool markdownWins)
    {
        var fichaOutcomes = ReadOutcomes(baseLines, fichaLines, out var fichaAdded);
        var markdownOutcomes = ReadOutcomes(baseLines, markdownLines, out var markdownAdded);
        var merged = new List<string>();
        var synced = new List<string>();

        for (var index = 0; index < baseLines.Count; index++)
        {
            var changedInFicha = fichaOutcomes.TryGetValue(index, out var fichaValue);
            var changedInMarkdown = markdownOutcomes.TryGetValue(index, out var markdownValue);
            string? value;
            string? syncedValue;

            if (!changedInFicha && !changedInMarkdown)
            {
                value = syncedValue = baseLines[index];
            }
            else if (!changedInMarkdown)
            {
                // Only the ficha changed it: still pending to be written to the markdown.
                value = fichaValue;
                syncedValue = baseLines[index];
            }
            else if (!changedInFicha)
            {
                value = syncedValue = markdownValue;
            }
            else if (string.Equals(fichaValue, markdownValue, StringComparison.Ordinal))
            {
                value = syncedValue = fichaValue;
            }
            else if (markdownWins)
            {
                value = syncedValue = markdownValue;
            }
            else
            {
                // The ficha won: the markdown version was not adopted, so the conflict stays visible next time.
                value = fichaValue;
                syncedValue = baseLines[index];
            }

            if (value is not null)
            {
                merged.Add(value);
            }

            if (syncedValue is not null)
            {
                synced.Add(syncedValue);
            }
        }

        merged.AddRange(markdownAdded);
        synced.AddRange(markdownAdded);
        var addedByMarkdown = CountLines(markdownAdded);

        foreach (var line in fichaAdded)
        {
            // A note added on both sides (e.g. pulled earlier from the other side) is kept once.
            if (addedByMarkdown.TryGetValue(line, out var count) && count > 0)
            {
                addedByMarkdown[line] = count - 1;
                continue;
            }

            merged.Add(line);
        }

        return new NoteLinesMergeResult(merged, synced);
    }

    private static Dictionary<int, string?> ReadOutcomes(
        IReadOnlyList<string> baseLines,
        IReadOnlyList<string> otherLines,
        out List<string> addedLines)
    {
        var outcomes = new Dictionary<int, string?>();
        addedLines = [];

        foreach (var edit in Diff(baseLines, otherLines))
        {
            if (edit.BaseIndex is { } baseIndex)
            {
                outcomes[baseIndex] = edit.NewIndex is { } newIndex ? otherLines[newIndex] : null;
            }
            else if (edit.NewIndex is { } addedIndex)
            {
                addedLines.Add(otherLines[addedIndex]);
            }
        }

        return outcomes;
    }

    private static Dictionary<int, List<int>> UnmatchedByGap(bool[] matched)
    {
        var byGap = new Dictionary<int, List<int>>();
        var matchedBefore = 0;

        for (var index = 0; index < matched.Length; index++)
        {
            if (matched[index])
            {
                matchedBefore++;
                continue;
            }

            if (!byGap.TryGetValue(matchedBefore, out var indexes))
            {
                byGap[matchedBefore] = indexes = [];
            }

            indexes.Add(index);
        }

        return byGap;
    }

    private static Dictionary<string, int> CountLines(IEnumerable<string> lines)
    {
        return lines
            .GroupBy(line => line, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
    }
}
