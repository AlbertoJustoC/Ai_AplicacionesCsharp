using Ai_DailyTracking.Domain.Models;
using Ai_DailyTracking.Infrastructure;

namespace Ai_DailyTracking.Tests;

public sealed class MarkdownNotesMergeTests
{
    [Fact]
    public void Notes_with_the_same_visible_minute_keep_both_lines()
    {
        var folder = Path.Combine(Path.GetTempPath(), "AiDailyTrackingMd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "entry.md");
            File.WriteAllText(path, "## Notas\n[2026-10-07 10:00] Nota A\n");
            var entry = new TrackingEntry();
            entry.Values["notes"] = "[2026-10-07 10:00] Nota B";

            var success = new TrackingEntryMarkdownExporter()
                .TryCreateOrUpdate(path, string.Empty, entry, out var notes, out var error);

            Assert.True(success, error);
            Assert.Contains("Nota A", notes);
            Assert.Contains("Nota B", notes);
            Assert.Contains("Nota A", File.ReadAllText(path));
            Assert.Contains("Nota B", File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
