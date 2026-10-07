using Ai_DailyTracking.Domain.Models;
using Ai_DailyTracking.Infrastructure;

namespace Ai_DailyTracking.Tests;

public sealed class ProjectPresenceStoreTests
{
    [Fact]
    public void Presence_lists_other_sessions_and_ignores_stale_or_closed_sessions()
    {
        var root = Path.Combine(Path.GetTempPath(), "AiDailyTrackingPresence-" + Guid.NewGuid().ToString("N"));
        var json = new JsonFileStore();
        var first = new ProjectPresenceStore(json);
        var second = new ProjectPresenceStore(json);
        var projectId = Guid.NewGuid();
        try
        {
            Assert.Empty(first.Open(projectId, root));
            Assert.Single(second.Open(projectId, root));
            var directory = Path.Combine(root, ".AiDailyTrackingChanges", projectId.ToString("N"), "presence");
            var stale = new ProjectPresence
            {
                SessionId = Guid.NewGuid(), ProjectId = projectId,
                UserName = "Old user", UpdatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-30)
            };
            json.Write(Path.Combine(directory, stale.SessionId.ToString("N") + ".json"), stale);

            Assert.Single(second.Heartbeat(projectId, root));
            first.Close();
            Assert.Empty(second.Heartbeat(projectId, root));
        }
        finally
        {
            first.Close();
            second.Close();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
