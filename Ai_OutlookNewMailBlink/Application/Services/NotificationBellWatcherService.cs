using System.Text.RegularExpressions;
using System.Windows.Automation;
using Ai_OutlookNewMailBlink.Domain.Models;
using Ai_OutlookNewMailBlink.Infrastructure.Win32;

namespace Ai_OutlookNewMailBlink.Application.Services;

public sealed class NewNotificationDetectedEventArgs : EventArgs
{
    public required int NotificationCount { get; init; }

    public required IntPtr OutlookWindowHandle { get; init; }
}

// Polls the Windows taskbar's "Notifications" bell button (via UI Automation) for its new-item
// count and raises NewNotificationDetected whenever it goes up. This targets the taskbar/Shell
// instead of Outlook's own window because the new Outlook is WebView2/Chromium-hosted and does
// not expose its inbox content through UI Automation - the Shell notification bell does, and new
// Outlook mail toasts go through it. Trade-off: this fires for ANY new Windows notification, not
// only Outlook mail.
public sealed class NotificationBellWatcherService
{
    private static readonly Regex FirstNumberPattern = new(@"\d+", RegexOptions.Compiled);
    private const int MaxSearchDepth = 8;
    private const int MaxNodesSearched = 5000;

    private readonly OutlookProcessLocator _processLocator;
    private readonly BlinkSettings _settings;

    private int? _lastKnownNotificationCount;

    public event EventHandler<NewNotificationDetectedEventArgs>? NewNotificationDetected;

    public NotificationBellWatcherService(OutlookProcessLocator processLocator, BlinkSettings settings)
    {
        _processLocator = processLocator;
        _settings = settings;
    }

    public void Poll()
    {
        var notificationCount = TryReadNotificationCount();
        if (notificationCount is null)
        {
            return;
        }

        if (_lastKnownNotificationCount.HasValue && notificationCount.Value > _lastKnownNotificationCount.Value)
        {
            var outlookWindow = _processLocator.TryFindOutlookWindow();

            NewNotificationDetected?.Invoke(this, new NewNotificationDetectedEventArgs
            {
                NotificationCount = notificationCount.Value,
                OutlookWindowHandle = outlookWindow?.MainWindowHandle ?? IntPtr.Zero
            });
        }

        _lastKnownNotificationCount = notificationCount.Value;
    }

    private int? TryReadNotificationCount()
    {
        try
        {
            var taskbarHandle = NativeMethods.FindWindow("Shell_TrayWnd", null);
            if (taskbarHandle == IntPtr.Zero)
            {
                return null;
            }

            var root = AutomationElement.FromHandle(taskbarHandle);
            var nodeCount = 0;
            return SearchForNotificationBell(root, 0, ref nodeCount);
        }
        catch (Exception)
        {
            // UI Automation over live Shell elements is inherently fragile.
            return null;
        }
    }

    private int? SearchForNotificationBell(AutomationElement element, int depth, ref int nodeCount)
    {
        if (depth > MaxSearchDepth || nodeCount > MaxNodesSearched)
        {
            return null;
        }

        nodeCount++;

        string? name;
        try
        {
            name = element.Current.Name;
        }
        catch (ElementNotAvailableException)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(name))
        {
            foreach (var bellNamePrefix in _settings.NotificationBellNamePrefixes)
            {
                if (name.StartsWith(bellNamePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var match = FirstNumberPattern.Match(name);
                    return match.Success && int.TryParse(match.Value, out var count) ? count : 0;
                }
            }
        }

        AutomationElement? child;
        try
        {
            child = TreeWalker.ControlViewWalker.GetFirstChild(element);
        }
        catch (ElementNotAvailableException)
        {
            return null;
        }

        while (child is not null)
        {
            var found = SearchForNotificationBell(child, depth + 1, ref nodeCount);
            if (found.HasValue)
            {
                return found;
            }

            try
            {
                child = TreeWalker.ControlViewWalker.GetNextSibling(child);
            }
            catch (ElementNotAvailableException)
            {
                break;
            }
        }

        return null;
    }
}
