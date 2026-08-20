using Ai_preAgresso.Application.Services;
using Ai_preAgresso.Infrastructure;
using Ai_preAgresso.Shared.Constants;
using Ai_preAgresso.UI.Forms;

namespace Ai_preAgresso;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var jsonFileStore = new JsonFileStore();
        var preferencesRepository = new AppPreferencesRepository(jsonFileStore);
        var initialProjectFilePath = preferencesRepository.Load().LastProjectFilePath;
        if (string.IsNullOrWhiteSpace(initialProjectFilePath))
        {
            initialProjectFilePath = AppStoragePaths.EntriesFile;
        }

        var repository = new TimeEntryRepository(jsonFileStore, initialProjectFilePath);
        var workspaceService = new AgressoWorkspaceService(repository, preferencesRepository);
        var excelService = new TimeEntryExcelService();

        System.Windows.Forms.Application.Run(new MainForm(workspaceService, excelService));
    }
}
