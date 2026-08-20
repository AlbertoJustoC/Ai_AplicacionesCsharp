using Ai_preAgresso.Domain.Models;
using Ai_preAgresso.Shared.Constants;

namespace Ai_preAgresso.Infrastructure;

public sealed class AppPreferencesRepository
{
    private readonly JsonFileStore _store;

    public AppPreferencesRepository(JsonFileStore store)
    {
        _store = store;
    }

    public AppPreferences Load() => _store.ReadOrDefault(AppStoragePaths.PreferencesFile, new AppPreferences());

    public void Save(AppPreferences preferences) => _store.Write(AppStoragePaths.PreferencesFile, preferences);
}
