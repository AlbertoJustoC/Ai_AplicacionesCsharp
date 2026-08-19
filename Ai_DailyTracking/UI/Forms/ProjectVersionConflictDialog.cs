namespace Ai_DailyTracking.UI.Forms;

// Lets the user pick which version of a project to keep when the app's copy and the destination folder's copy differ.
public sealed class ProjectVersionConflictDialog : Form
{
    public enum ConflictChoice
    {
        Cancel,
        UseAppVersion,
        UseFolderVersion
    }

    public ConflictChoice Choice { get; private set; } = ConflictChoice.Cancel;

    public ProjectVersionConflictDialog(string projectName, DateTime appUpdatedAtLocal, DateTime folderUpdatedAtLocal)
    {
        var folderIsNewer = folderUpdatedAtLocal >= appUpdatedAtLocal;

        Text = "Version distinta de proyecto";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(480, 284);
        BackColor = Color.FromArgb(245, 247, 250);
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);

        var titleLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 44,
            Text = "Version distinta de proyecto",
            Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold, GraphicsUnit.Point),
            Padding = new Padding(20, 14, 20, 0)
        };

        var messageLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 50,
            Text = $"El proyecto \"{projectName}\" tiene una version distinta en la aplicacion y en la carpeta destino. Elige con cual quedarte:",
            ForeColor = Color.FromArgb(88, 96, 110),
            Padding = new Padding(20, 0, 20, 0)
        };

        var appButton = CreateOptionButton(
            $"Usar version de la aplicacion{(!folderIsNewer ? "  (mas reciente)" : string.Empty)}",
            $"Ultimo cambio: {appUpdatedAtLocal:dd/MM/yyyy HH:mm:ss}",
            isRecommended: false);
        appButton.Top = 100;
        appButton.Click += (_, _) => Finish(ConflictChoice.UseAppVersion);

        var folderButton = CreateOptionButton(
            $"Usar version de la carpeta destino (recomendado){(folderIsNewer ? "  (mas reciente)" : string.Empty)}",
            $"Ultimo cambio: {folderUpdatedAtLocal:dd/MM/yyyy HH:mm:ss}",
            isRecommended: true);
        folderButton.Top = 160;
        folderButton.Click += (_, _) => Finish(ConflictChoice.UseFolderVersion);

        var buttonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12)
        };

        var cancelButton = new Button
        {
            Text = "Cancelar (no cambiar de carpeta)",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            Padding = new Padding(12, 6, 12, 6),
            DialogResult = DialogResult.Cancel
        };
        cancelButton.Click += (_, _) => Choice = ConflictChoice.Cancel;

        buttonsPanel.Controls.Add(cancelButton);

        Controls.Add(appButton);
        Controls.Add(folderButton);
        Controls.Add(buttonsPanel);
        Controls.Add(messageLabel);
        Controls.Add(titleLabel);

        AcceptButton = folderButton;
        CancelButton = cancelButton;
    }

    private void Finish(ConflictChoice choice)
    {
        Choice = choice;
        DialogResult = DialogResult.OK;
        Close();
    }

    private static Button CreateOptionButton(string title, string subtitle, bool isRecommended)
    {
        var button = new Button
        {
            Left = 20,
            Width = 440,
            Height = 52,
            Text = $"{title}\n{subtitle}",
            TextAlign = ContentAlignment.MiddleLeft,
            FlatStyle = FlatStyle.Flat,
            BackColor = isRecommended ? Color.FromArgb(18, 103, 177) : Color.White,
            ForeColor = isRecommended ? Color.White : Color.FromArgb(40, 44, 52),
            Padding = new Padding(12, 0, 0, 0)
        };
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = isRecommended ? Color.FromArgb(18, 103, 177) : Color.FromArgb(200, 205, 212);
        return button;
    }
}
