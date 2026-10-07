using Ai_DailyTracking.Domain.Models;
using System.Text;

namespace Ai_DailyTracking.Infrastructure;

// Presence is advisory. OneDrive may deliver these files after another editor has already saved.
public sealed class ProjectPresenceStore
{
    private static readonly TimeSpan ActiveWindow = TimeSpan.FromMinutes(10);
    private readonly JsonFileStore _json;
    private readonly Guid _sessionId = Guid.NewGuid();
    private string? _ownPath;

    public ProjectPresenceStore(JsonFileStore json) => _json = json;

    public IReadOnlyList<ProjectPresence> Open(Guid projectId, string folder)
    {
        if (_ownPath is not null && !string.Equals(_ownPath, PresencePath(folder, projectId), StringComparison.OrdinalIgnoreCase))
            Close();
        return Heartbeat(projectId, folder);
    }

    public IReadOnlyList<ProjectPresence> Heartbeat(Guid projectId, string folder)
    {
        var path = PresencePath(folder, projectId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var presence = new ProjectPresence
        {
            SessionId = _sessionId,
            ProjectId = projectId,
            UserName = Environment.UserName,
            MachineName = Environment.MachineName,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, _json.Serialize(presence), Encoding.UTF8);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
        _ownPath = path;
        var now = DateTimeOffset.UtcNow;
        return Directory.GetFiles(Path.GetDirectoryName(path)!, "*.json")
            .Where(item => !string.Equals(item, path, StringComparison.OrdinalIgnoreCase))
            .Select(ReadPresence)
            .OfType<ProjectPresence>()
            .Where(item => item.ProjectId == projectId && item.SessionId != _sessionId &&
                item.UpdatedAtUtc <= now.AddMinutes(1) && now - item.UpdatedAtUtc <= ActiveWindow)
            .OrderBy(item => item.UserName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void Close()
    {
        if (_ownPath is { } path && File.Exists(path)) File.Delete(path);
        _ownPath = null;
    }

    private ProjectPresence? ReadPresence(string path)
    {
        try { return _json.ReadOrDefault<ProjectPresence?>(path, null); }
        catch (IOException) { return null; }
        catch (System.Text.Json.JsonException) { return null; }
    }

    private string PresencePath(string folder, Guid projectId) =>
        Path.Combine(folder, ".AiDailyTrackingChanges", projectId.ToString("N"), "presence", _sessionId.ToString("N") + ".json");
}
