using System.Diagnostics;

namespace Ai_NewNotificationBlink.Application.Services;

public sealed class OutlookWindowInfo
{
    public required Process Process { get; init; }

    public required IntPtr MainWindowHandle { get; init; }
}

// Locates the running Outlook process (new Outlook = "olk", classic Outlook = "OUTLOOK").
public sealed class OutlookProcessLocator
{
    private static readonly string[] ProcessNames = { "olk", "OUTLOOK" };

    public OutlookWindowInfo? TryFindOutlookWindow()
    {
        foreach (var processName in ProcessNames)
        {
            var candidates = Process.GetProcessesByName(processName);

            foreach (var process in candidates)
            {
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    return new OutlookWindowInfo
                    {
                        Process = process,
                        MainWindowHandle = process.MainWindowHandle
                    };
                }
            }
        }

        return null;
    }
}
