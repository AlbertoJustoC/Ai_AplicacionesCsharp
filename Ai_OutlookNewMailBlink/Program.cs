using Ai_OutlookNewMailBlink.UI;

namespace Ai_OutlookNewMailBlink;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        System.Windows.Forms.Application.Run(new TrayApplicationContext());
    }
}
