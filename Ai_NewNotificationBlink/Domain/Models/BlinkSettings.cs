namespace Ai_NewNotificationBlink.Domain.Models;

public sealed class BlinkSettings
{
    public int PollIntervalSeconds { get; set; } = 5;

    public bool FlashOutlookWindowEnabled { get; set; } = false;

    public bool ShowAuxiliaryOverlayEnabled { get; set; } = true;

    public bool PlaySoundEnabled { get; set; } = false;

    // Localized name prefixes of the taskbar "Notifications" bell button, used to read its
    // new-notification count via UI Automation.
    public List<string> NotificationBellNamePrefixes { get; set; } = new()
    {
        "Notificaciones",
        "Notifications"
    };
}
