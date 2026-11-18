using Ai_DailyTracking.Domain.Models;
using Ai_DailyTracking.Infrastructure;

namespace Ai_DailyTracking.Tests;

public sealed class MarkdownNotesMergeTests
{
    private const string NoteA = "[2026-10-07 10:00] Nota A";
    private const string NoteB = "[2026-10-07 11:00] Nota B";

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

    [Fact]
    public void A_note_deleted_in_the_ficha_is_removed_from_the_markdown()
    {
        using var workspace = new Workspace($"{NoteA}\n{NoteB}", $"{NoteA}\n{NoteB}");
        workspace.Sync();

        workspace.Entry.Values["notes"] = NoteA;
        workspace.Sync();

        Assert.Equal([NoteA], workspace.MarkdownNotes());
        Assert.Equal([NoteA], workspace.FichaNotes());
    }

    [Fact]
    public void A_note_edited_in_the_ficha_replaces_the_old_text_in_the_markdown()
    {
        using var workspace = new Workspace($"{NoteA}\n{NoteB}", $"{NoteA}\n{NoteB}");
        workspace.Sync();

        workspace.Entry.Values["notes"] = $"{NoteA}\n{NoteB} corregida";
        workspace.Sync();

        Assert.Equal([NoteA, $"{NoteB} corregida"], workspace.MarkdownNotes());
    }

    [Fact]
    public void A_note_deleted_in_the_markdown_is_removed_from_the_ficha()
    {
        using var workspace = new Workspace($"{NoteA}\n{NoteB}", $"{NoteA}\n{NoteB}");
        workspace.Sync();

        workspace.WriteMarkdownNotes(NoteA);

        Assert.True(workspace.TryPull(out var pulled));
        Assert.Equal(NoteA, pulled);
    }

    [Fact]
    public void A_note_edited_in_the_markdown_replaces_the_old_text_in_the_ficha()
    {
        using var workspace = new Workspace($"{NoteA}\n{NoteB}", $"{NoteA}\n{NoteB}");
        workspace.Sync();

        workspace.WriteMarkdownNotes($"{NoteA}\n{NoteB} corregida");

        Assert.True(workspace.TryPull(out var pulled));
        Assert.Equal([NoteA, $"{NoteB} corregida"], pulled.Split(Environment.NewLine));
    }

    [Fact]
    public void A_note_added_in_the_markdown_can_later_be_deleted_from_the_ficha()
    {
        using var workspace = new Workspace(NoteA, NoteA);
        workspace.Sync();
        workspace.WriteMarkdownNotes($"{NoteA}\n{NoteB}");
        Assert.True(workspace.TryPull(out var pulled));
        workspace.Entry.Values["notes"] = pulled;
        workspace.Exporter.CommitPendingNotesBases();

        workspace.Entry.Values["notes"] = NoteA;
        workspace.Sync();

        Assert.Equal([NoteA], workspace.MarkdownNotes());
    }

    [Theory]
    [InlineData(true, "Version ficha")]
    [InlineData(false, "Version markdown")]
    public void When_both_sides_edit_the_same_note_the_most_recent_update_wins(bool fichaIsNewer, string expected)
    {
        using var workspace = new Workspace(NoteB, NoteB);
        workspace.Sync();
        workspace.Entry.Values["notes"] = "[2026-10-07 11:00] Version ficha";
        workspace.WriteMarkdownNotes("[2026-10-07 11:00] Version markdown");
        var markdownSavedAt = DateTimeOffset.UtcNow.AddHours(-1);
        File.SetLastWriteTimeUtc(workspace.MarkdownPath, markdownSavedAt.UtcDateTime);
        var fichaEditedAt = fichaIsNewer ? markdownSavedAt.AddMinutes(30) : markdownSavedAt.AddMinutes(-30);

        workspace.Sync(fichaEditedAt);

        Assert.EndsWith(expected, Assert.Single(workspace.MarkdownNotes()));
        Assert.EndsWith(expected, Assert.Single(workspace.FichaNotes()));
    }

    [Fact]
    public void An_empty_notes_section_in_the_markdown_does_not_wipe_the_ficha()
    {
        using var workspace = new Workspace($"{NoteA}\n{NoteB}", $"{NoteA}\n{NoteB}");
        workspace.Sync();

        workspace.WriteMarkdownNotes(string.Empty);

        Assert.False(workspace.TryPull(out _));
        workspace.Sync();
        Assert.Equal([NoteA, NoteB], workspace.MarkdownNotes());
    }

    private sealed class Workspace : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "AiDailyTrackingMd-" + Guid.NewGuid().ToString("N"));

        public Workspace(string markdownNotes, string fichaNotes)
        {
            Directory.CreateDirectory(_folder);
            MarkdownPath = Path.Combine(_folder, "entry.md");
            WriteMarkdownNotes(markdownNotes);
            Exporter = new TrackingEntryMarkdownExporter(new NotesSyncStateStore(new JsonFileStore(), Path.Combine(_folder, "state")));
            Entry.Values["notes"] = fichaNotes;
        }

        public string MarkdownPath { get; }
        public TrackingEntryMarkdownExporter Exporter { get; }
        public TrackingEntry Entry { get; } = new();

        public void WriteMarkdownNotes(string notes) => File.WriteAllText(MarkdownPath, "## Notas\n" + notes + "\n");

        public string[] MarkdownNotes() => File.ReadAllLines(MarkdownPath)
            .SkipWhile(line => line != "## Notas").Skip(1).Where(line => line.Length > 0).ToArray();

        public string[] FichaNotes() => (Entry.Values["notes"] ?? string.Empty).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        public bool TryPull(out string pulledNotes) => Exporter.TryPullNotes(MarkdownPath, Entry, null, out pulledNotes);

        // Same as the "Actualizar MD" button: the merged notes go back into the ficha.
        public void Sync(DateTimeOffset? notesModifiedAtUtc = null)
        {
            Assert.True(Exporter.TryCreateOrUpdate(MarkdownPath, string.Empty, Entry, notesModifiedAtUtc, out var notes, out var error), error);
            Entry.Values["notes"] = notes;
        }

        public void Dispose() => Directory.Delete(_folder, recursive: true);
    }
}
