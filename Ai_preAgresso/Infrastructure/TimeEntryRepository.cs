using Ai_preAgresso.Domain.Models;

namespace Ai_preAgresso.Infrastructure;

public sealed class TimeEntryRepository
{
    private readonly JsonFileStore _store;

    public string CurrentFilePath { get; private set; }

    public TimeEntryRepository(JsonFileStore store, string initialFilePath)
    {
        _store = store;
        CurrentFilePath = initialFilePath;
    }

    public List<TimeEntry> LoadAll() => _store.ReadOrDefault(CurrentFilePath, new List<TimeEntry>());

    public void SaveAll(List<TimeEntry> entries) => _store.Write(CurrentFilePath, entries);

    public void SetFilePath(string filePath) => CurrentFilePath = filePath;
}
