using Ai_preAgresso.Domain.Models;
using Ai_preAgresso.Shared.Constants;

namespace Ai_preAgresso.Infrastructure;

public sealed class TimeEntryRepository
{
    private readonly JsonFileStore _store;

    public TimeEntryRepository(JsonFileStore store)
    {
        _store = store;
    }

    public List<TimeEntry> LoadAll() => _store.ReadOrDefault(AppStoragePaths.EntriesFile, new List<TimeEntry>());

    public void SaveAll(List<TimeEntry> entries) => _store.Write(AppStoragePaths.EntriesFile, entries);
}
