using System.Text;

namespace Ai_DailyTracking.Shared.Helpers;

public static class ProjectFileNameHelper
{
    public static string BuildStorageFileName(string projectName, Guid projectId)
    {
        var safeName = Sanitize(projectName);
        return $"{safeName}-{projectId:N}.json";
    }

    public static string BuildBackupFileName(string projectName, DateTime timestampLocal)
    {
        var safeName = Sanitize(projectName);
        return $"{safeName}-{timestampLocal:yyyyMMdd-HHmmss}.json";
    }

    private static string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "project";
        }

        var builder = new StringBuilder(value.Length);

        foreach (var character in value.Trim())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                continue;
            }

            if (character is ' ' or '-' or '_')
            {
                builder.Append('-');
            }
        }

        var normalized = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? "project" : normalized;
    }
}