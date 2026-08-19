using Ai_preAgresso.Application.Services;
using Ai_preAgresso.Infrastructure;
using Ai_preAgresso.UI.Forms;

namespace Ai_preAgresso;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var jsonFileStore = new JsonFileStore();
        var repository = new TimeEntryRepository(jsonFileStore);
        var workspaceService = new AgressoWorkspaceService(repository);
        var excelService = new TimeEntryExcelService();

        System.Windows.Forms.Application.Run(new MainForm(workspaceService, excelService));
    }
}
