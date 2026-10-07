namespace Ai_DailyTracking.Shared.Helpers;

public static class FileNameSanitizer
{
    public static string SanitizePart(string? value, string fallback)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string((value ?? string.Empty)
            .Trim()
            .Select(character => invalidChars.Contains(character) ? '_' : character)
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
    }
}
