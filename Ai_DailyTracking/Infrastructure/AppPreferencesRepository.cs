using Ai_DailyTracking.Domain.Models;
using Ai_DailyTracking.Shared.Constants;

namespace Ai_DailyTracking.Infrastructure;

public sealed class AppPreferencesRepository
{
    private readonly JsonFileStore _jsonFileStore;
    private readonly string _preferencesFile;

    public AppPreferencesRepository(JsonFileStore jsonFileStore, string? preferencesFile = null)
    {
        _jsonFileStore = jsonFileStore;
        _preferencesFile = preferencesFile ?? AppStoragePaths.PreferencesFile;
    }

    public AppPreferences Load()
    {
        return _jsonFileStore.ReadOrDefault(_preferencesFile, new AppPreferences());
    }

    public void Save(AppPreferences preferences)
    {
        _jsonFileStore.Write(_preferencesFile, preferences);
    }
}
