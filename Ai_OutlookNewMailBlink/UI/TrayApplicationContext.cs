using Ai_OutlookNewMailBlink.Application.Services;
using Ai_OutlookNewMailBlink.Infrastructure;
using Ai_OutlookNewMailBlink.Shared.Constants;
using Ai_OutlookNewMailBlink.Shared.Helpers;
using Ai_OutlookNewMailBlink.UI.Forms;

namespace Ai_OutlookNewMailBlink.UI;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly BlinkSettingsRepository _settingsRepository;
    private readonly OutlookProcessLocator _processLocator = new();
    private readonly WindowFlashService _flashService = new();

    private NotificationBellWatcherService _watcherService;
    private readonly System.Windows.Forms.Timer _pollTimer;
    private readonly FlashOverlayForm _overlayForm;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _toggleMonitoringMenuItem;

    private bool _isMonitoring;

    public TrayApplicationContext()
    {
        _settingsRepository = new BlinkSettingsRepository(new JsonFileStore());
        var settings = _settingsRepository.Load();

        _watcherService = new NotificationBellWatcherService(_processLocator, settings);
        _watcherService.NewNotificationDetected += OnNewNotificationDetected;

        _overlayForm = new FlashOverlayForm(_flashService);

        _pollTimer = new System.Windows.Forms.Timer { Interval = settings.PollIntervalSeconds * 1000 };
        _pollTimer.Tick += (_, _) => _watcherService.Poll();

        _toggleMonitoringMenuItem = new ToolStripMenuItem("Iniciar monitorización", null, (_, _) => ToggleMonitoring());

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(_toggleMonitoringMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Probar ventana auxiliar", null, (_, _) => _overlayForm.ShowAndFlash(1));
        contextMenu.Items.Add("Volcar árbol de UI Automation (diagnóstico)", null, (_, _) => DumpAutomationTree());
        contextMenu.Items.Add("Volcar árbol de la barra de tareas (diagnóstico)", null, (_, _) => DumpTaskbarTree());
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Configuración...", null, (_, _) => OpenSettings());
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Salir", null, (_, _) => ExitApplication());

        _notifyIcon = new NotifyIcon
        {
            Icon = AppIconProvider.Current,
            Text = "Ai_OutlookNewMailBlink",
            ContextMenuStrip = contextMenu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => ToggleMonitoring();

        TryEnableScreenReaderHint();
        ToggleMonitoring();
    }

    // The new Outlook is WebView2/Chromium-hosted, which only builds its full UI Automation
    // accessibility tree once it detects an assistive technology is present - this hints that.
    private static void TryEnableScreenReaderHint()
    {
        Infrastructure.Win32.NativeMethods.SystemParametersInfo(
            Infrastructure.Win32.NativeMethods.SPI_SETSCREENREADER, 1, IntPtr.Zero, Infrastructure.Win32.NativeMethods.SPIF_SENDCHANGE);
    }

    private void ToggleMonitoring()
    {
        _isMonitoring = !_isMonitoring;

        if (_isMonitoring)
        {
            _pollTimer.Start();
            _toggleMonitoringMenuItem.Text = "Detener monitorización";
            _notifyIcon.Text = "Ai_OutlookNewMailBlink - monitorizando";
        }
        else
        {
            _pollTimer.Stop();
            _toggleMonitoringMenuItem.Text = "Iniciar monitorización";
            _notifyIcon.Text = "Ai_OutlookNewMailBlink - detenido";
        }
    }

    private void OnNewNotificationDetected(object? sender, NewNotificationDetectedEventArgs e)
    {
        var settings = _settingsRepository.Load();

        if (settings.FlashOutlookWindowEnabled && e.OutlookWindowHandle != IntPtr.Zero)
        {
            _flashService.StartFlashing(e.OutlookWindowHandle);
        }

        if (settings.ShowAuxiliaryOverlayEnabled)
        {
            _overlayForm.ShowAndFlash(e.NotificationCount);
        }

        if (settings.PlaySoundEnabled)
        {
            System.Media.SystemSounds.Asterisk.Play();
        }
    }

    private void DumpAutomationTree()
    {
        var outlookWindow = _processLocator.TryFindOutlookWindow();
        if (outlookWindow is null)
        {
            MessageBox.Show("No se ha encontrado ninguna ventana de Outlook en ejecución.", "Ai_OutlookNewMailBlink",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        AutomationTreeDumper.DumpToFile(outlookWindow.MainWindowHandle, AppStoragePaths.AutomationTreeDumpFile);
        MessageBox.Show($"Árbol volcado en:\n{AppStoragePaths.AutomationTreeDumpFile}", "Ai_OutlookNewMailBlink",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static void DumpTaskbarTree()
    {
        AutomationTreeDumper.DumpTaskbarToFile(AppStoragePaths.TaskbarTreeDumpFile);
        MessageBox.Show($"Árbol de la barra de tareas volcado en:\n{AppStoragePaths.TaskbarTreeDumpFile}", "Ai_OutlookNewMailBlink",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OpenSettings()
    {
        var currentSettings = _settingsRepository.Load();
        using var settingsForm = new SettingsForm(currentSettings);

        if (settingsForm.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        _settingsRepository.Save(settingsForm.Result);
        _watcherService = new NotificationBellWatcherService(_processLocator, settingsForm.Result);
        _watcherService.NewNotificationDetected += OnNewNotificationDetected;
        _pollTimer.Interval = settingsForm.Result.PollIntervalSeconds * 1000;
    }

    private void ExitApplication()
    {
        _pollTimer.Stop();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _overlayForm.Dispose();
        System.Windows.Forms.Application.Exit();
    }
}
