using Ai_DailyTracking.Application.Services;
using Ai_DailyTracking.Infrastructure;
using Ai_DailyTracking.UI.Forms;

namespace Ai_DailyTracking;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var jsonFileStore = new JsonFileStore();
        var preferencesRepository = new AppPreferencesRepository(jsonFileStore);
        var projectRepository = new TrackingProjectRepository(jsonFileStore, preferencesRepository);
        var schemaRepository = new TrackingSchemaRepository(jsonFileStore);
        var workspaceService = new ProjectWorkspaceService(projectRepository, preferencesRepository, schemaRepository);

        System.Windows.Forms.Application.Run(new DailyTrackingForm(workspaceService));
    }
}