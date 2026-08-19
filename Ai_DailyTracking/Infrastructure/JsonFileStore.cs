using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ai_DailyTracking.Infrastructure;

public sealed class JsonFileStore
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public T ReadOrDefault<T>(string filePath, T defaultValue)
    {
        if (!File.Exists(filePath))
        {
            return defaultValue;
        }

        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<T>(json, _serializerOptions) ?? defaultValue;
    }

    public void Write<T>(string filePath, T value)
    {
        var directoryPath = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var json = JsonSerializer.Serialize(value, _serializerOptions);
        File.WriteAllText(filePath, json);
    }
}