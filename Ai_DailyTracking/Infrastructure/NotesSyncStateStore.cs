using Ai_DailyTracking.Shared.Constants;

namespace Ai_DailyTracking.Infrastructure;

// Per-computer record of the notes of each ficha as they were the last time ficha and Markdown were known to
// agree. It is the common ancestor that lets a three-way merge tell a deleted/edited note from a new one.
// It is local on purpose: the Markdown path (and so what "synchronized" means) differs between computers.
public sealed class NotesSyncStateStore
{
    private readonly JsonFileStore _json;
    private readonly string _filePath;
    private readonly object _gate = new();

    public NotesSyncStateStore(JsonFileStore json, string? workingDirectory = null)
    {
        _json = json;
        _filePath = Path.Combine(workingDirectory ?? Path.Combine(AppStoragePaths.RootDirectory, "Working"), "notes-sync.json");
    }

    public IReadOnlyList<string>? TryGet(Guid entryId, string markdownFilePath)
    {
        lock (_gate)
        {
            var states = Read();

            return states.TryGetValue(Key(entryId), out var state) &&
                   string.Equals(state.MarkdownPath, NormalizePath(markdownFilePath), StringComparison.OrdinalIgnoreCase)
                ? state.Lines
                : null;
        }
    }

    public void Save(Guid entryId, string markdownFilePath, IReadOnlyList<string> lines)
    {
        lock (_gate)
        {
            var states = Read();
            states[Key(entryId)] = new NotesSyncState
            {
                MarkdownPath = NormalizePath(markdownFilePath),
                Lines = lines.ToList(),
                SyncedAtUtc = DateTimeOffset.UtcNow
            };

            _json.Write(_filePath, states);
        }
    }

    private Dictionary<string, NotesSyncState> Read()
    {
        try
        {
            var states = _json.ReadOrDefault(_filePath, new Dictionary<string, NotesSyncState>());
            return new Dictionary<string, NotesSyncState>(states, StringComparer.Ordinal);
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or IOException)
        {
            return new Dictionary<string, NotesSyncState>(StringComparer.Ordinal);
        }
    }

    private static string Key(Guid entryId) => entryId.ToString("N");

    private static string NormalizePath(string path) => Path.GetFullPath(path);

    public sealed class NotesSyncState
    {
        public string MarkdownPath { get; set; } = string.Empty;
        public List<string> Lines { get; set; } = [];
        public DateTimeOffset SyncedAtUtc { get; set; }
    }
}
