using Ai_NewNotificationBlink.UI;

namespace Ai_NewNotificationBlink;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        System.Windows.Forms.Application.Run(new TrayApplicationContext());
    }
}
