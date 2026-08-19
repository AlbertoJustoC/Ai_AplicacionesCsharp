using Ai_DailyTracking.Domain.Models;
using Ai_DailyTracking.Shared.Constants;

namespace Ai_DailyTracking.Infrastructure;

public sealed class AppPreferencesRepository
{
    private readonly JsonFileStore _jsonFileStore;

    public AppPreferencesRepository(JsonFileStore jsonFileStore)
    {
        _jsonFileStore = jsonFileStore;
    }

    public AppPreferences Load()
    {
        return _jsonFileStore.ReadOrDefault(AppStoragePaths.PreferencesFile, new AppPreferences());
    }

    public void Save(AppPreferences preferences)
    {
        _jsonFileStore.Write(AppStoragePaths.PreferencesFile, preferences);
    }
}