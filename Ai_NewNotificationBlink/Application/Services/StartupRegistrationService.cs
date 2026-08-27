using Microsoft.Win32;

namespace Ai_NewNotificationBlink.Application.Services;

// Registers/unregisters the app in the per-user "Run" registry key so it starts with Windows.
// HKCU (not HKLM) is used deliberately - no admin rights required.
public sealed class StartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Ai_NewNotificationBlink";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string existingValue
            && string.Equals(existingValue.Trim('"'), System.Windows.Forms.Application.ExecutablePath, StringComparison.OrdinalIgnoreCase);
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (key is null)
        {
            return;
        }

        if (enabled)
        {
            key.SetValue(ValueName, $"\"{System.Windows.Forms.Application.ExecutablePath}\"");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
