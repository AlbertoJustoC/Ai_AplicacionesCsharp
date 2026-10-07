using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ai_DailyTracking.Infrastructure;

public sealed class JsonFileStore
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public T ReadOrDefault<T>(string filePath, T defaultValue)
    {
        if (!File.Exists(filePath))
        {
            return defaultValue;
        }

        var json = File.ReadAllText(filePath);
        return Deserialize(json, defaultValue);
    }

    public void Write<T>(string filePath, T value)
    {
        var directoryPath = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var json = Serialize(value);
        File.WriteAllText(filePath, json);
    }

    public T Deserialize<T>(string json, T defaultValue) =>
        JsonSerializer.Deserialize<T>(json, _serializerOptions) ?? defaultValue;

    public string Serialize<T>(T value) => JsonSerializer.Serialize(value, _serializerOptions);
}
