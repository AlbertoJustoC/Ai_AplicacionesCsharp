using Ai_OutlookNewMailBlink.Domain.Models;
using Ai_OutlookNewMailBlink.Shared.Constants;

namespace Ai_OutlookNewMailBlink.Infrastructure;

public sealed class BlinkSettingsRepository
{
    private readonly JsonFileStore _jsonFileStore;

    public BlinkSettingsRepository(JsonFileStore jsonFileStore)
    {
        _jsonFileStore = jsonFileStore;
    }

    public BlinkSettings Load()
    {
        return _jsonFileStore.ReadOrDefault(AppStoragePaths.SettingsFile, new BlinkSettings());
    }

    public void Save(BlinkSettings settings)
    {
        _jsonFileStore.Write(AppStoragePaths.SettingsFile, settings);
    }
}
