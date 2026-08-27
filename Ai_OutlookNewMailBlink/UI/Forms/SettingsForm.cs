using Ai_OutlookNewMailBlink.Application.Services;
using Ai_OutlookNewMailBlink.Domain.Models;
using Ai_OutlookNewMailBlink.Shared.Helpers;

namespace Ai_OutlookNewMailBlink.UI.Forms;

public sealed class SettingsForm : Form
{
    private readonly StartupRegistrationService _startupRegistrationService = new();
    private readonly NumericUpDown _pollIntervalInput;
    private readonly CheckBox _flashOutlookCheckBox;
    private readonly CheckBox _showOverlayCheckBox;
    private readonly CheckBox _playSoundCheckBox;
    private readonly CheckBox _startWithWindowsCheckBox;
    private readonly TextBox _inboxFolderNamesInput;

    public BlinkSettings Result { get; private set; }

    public SettingsForm(BlinkSettings currentSettings)
    {
        Result = currentSettings;

        Text = "Configuración";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Width = 420;
        Height = 330;
        Icon = AppIconProvider.Current;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(12),
            AutoSize = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

        layout.Controls.Add(new Label { Text = "Intervalo de sondeo (segundos):", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        _pollIntervalInput = new NumericUpDown { Minimum = 2, Maximum = 300, Value = currentSettings.PollIntervalSeconds, Width = 80 };
        layout.Controls.Add(_pollIntervalInput, 1, 0);

        _flashOutlookCheckBox = new CheckBox { Text = "Parpadear la ventana de Outlook", AutoSize = true, Checked = currentSettings.FlashOutlookWindowEnabled };
        layout.Controls.Add(_flashOutlookCheckBox, 0, 1);
        layout.SetColumnSpan(_flashOutlookCheckBox, 2);

        _showOverlayCheckBox = new CheckBox { Text = "Mostrar ventana auxiliar de aviso", AutoSize = true, Checked = currentSettings.ShowAuxiliaryOverlayEnabled };
        layout.Controls.Add(_showOverlayCheckBox, 0, 2);
        layout.SetColumnSpan(_showOverlayCheckBox, 2);

        _playSoundCheckBox = new CheckBox { Text = "Reproducir sonido", AutoSize = true, Checked = currentSettings.PlaySoundEnabled };
        layout.Controls.Add(_playSoundCheckBox, 0, 3);
        layout.SetColumnSpan(_playSoundCheckBox, 2);

        _startWithWindowsCheckBox = new CheckBox { Text = "Iniciar automáticamente con Windows", AutoSize = true, Checked = _startupRegistrationService.IsEnabled() };
        layout.Controls.Add(_startWithWindowsCheckBox, 0, 4);
        layout.SetColumnSpan(_startWithWindowsCheckBox, 2);

        layout.Controls.Add(new Label { Text = "Nombres del icono de notificaciones (separados por ;):", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 5);
        layout.SetColumnSpan(layout.Controls[layout.Controls.Count - 1], 2);
        _inboxFolderNamesInput = new TextBox { Width = 380, Text = string.Join(";", currentSettings.NotificationBellNamePrefixes) };
        layout.Controls.Add(_inboxFolderNamesInput, 0, 6);
        layout.SetColumnSpan(_inboxFolderNamesInput, 2);

        var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 44 };
        var acceptButton = new Button { Text = "Aceptar", DialogResult = DialogResult.OK, Width = 90 };
        var cancelButton = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Width = 90 };
        acceptButton.Click += (_, _) => ApplyResult();
        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(acceptButton);

        Controls.Add(layout);
        Controls.Add(buttonPanel);

        AcceptButton = acceptButton;
        CancelButton = cancelButton;
    }

    private void ApplyResult()
    {
        _startupRegistrationService.SetEnabled(_startWithWindowsCheckBox.Checked);

        Result = new BlinkSettings
        {
            PollIntervalSeconds = (int)_pollIntervalInput.Value,
            FlashOutlookWindowEnabled = _flashOutlookCheckBox.Checked,
            ShowAuxiliaryOverlayEnabled = _showOverlayCheckBox.Checked,
            PlaySoundEnabled = _playSoundCheckBox.Checked,
            NotificationBellNamePrefixes = _inboxFolderNamesInput.Text
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList()
        };
    }
}
