using System.Text.RegularExpressions;

namespace Ai_DailyTracking.Shared.Helpers;

public enum NoteSegmentKind
{
    Text,
    Timestamp,
    Status,
    Link
}

public readonly record struct NoteSegment(NoteSegmentKind Kind, string Text);

// Splits the "notes" field text (one note per line: "[yyyy-MM-dd HH:mm] **Status** text") into the same
// colored pieces the ficha editor highlights, so other outputs (e.g. the report PDF) can reproduce them.
public static class NoteLineParser
{
    private static readonly Regex TokenRegex = new(
        $@"(?<link>{NoteLinkHelper.LinkPattern})|(?<timestamp>\[\d{{4}}-\d{{2}}-\d{{2}} \d{{2}}:\d{{2}}\])|\*\*(?<status>[^*\r\n]+)\*\*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<string> SplitLines(string? notesText)
    {
        return string.IsNullOrWhiteSpace(notesText)
            ? []
            : notesText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    // The status segment text is returned without its surrounding "**" markers, and a link segment carries only
    // its title (the url is dropped) so printed notes stay readable.
    public static IReadOnlyList<NoteSegment> Parse(string line)
    {
        var segments = new List<NoteSegment>();
        var position = 0;

        foreach (Match match in TokenRegex.Matches(line))
        {
            if (match.Index > position)
            {
                segments.Add(new NoteSegment(NoteSegmentKind.Text, line[position..match.Index]));
            }

            if (match.Groups["link"].Success)
            {
                var title = match.Groups["title"].Value.Trim();
                segments.Add(new NoteSegment(NoteSegmentKind.Link,
                    title.Length > 0 ? title : NoteLinkHelper.DefaultTitle(NoteLinkHelper.Classify(match.Groups["url"].Value))));
            }
            else
            {
                segments.Add(match.Groups["timestamp"].Success
                    ? new NoteSegment(NoteSegmentKind.Timestamp, match.Value)
                    : new NoteSegment(NoteSegmentKind.Status, match.Groups["status"].Value.Trim()));
            }

            position = match.Index + match.Length;
        }

        if (position < line.Length)
        {
            segments.Add(new NoteSegment(NoteSegmentKind.Text, line[position..]));
        }

        return segments;
    }
}
