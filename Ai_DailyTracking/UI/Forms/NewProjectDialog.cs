namespace Ai_DailyTracking.UI.Forms;

public sealed class NewProjectDialog : Form
{
    private readonly TextBox _projectNameTextBox;

    public NewProjectDialog()
    {
        Text = "Nuevo proyecto";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(440, 200);
        BackColor = Color.FromArgb(245, 247, 250);
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);

        var titleLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 44,
            Text = "Crear un proyecto de seguimiento",
            Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold, GraphicsUnit.Point),
            Padding = new Padding(20, 14, 20, 0)
        };

        var helpLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 34,
            Text = "El archivo JSON del proyecto se nombrara automaticamente con este nombre.",
            ForeColor = Color.FromArgb(88, 96, 110),
            Padding = new Padding(20, 0, 20, 0)
        };

        _projectNameTextBox = new TextBox
        {
            Left = 20,
            Top = 92,
            Width = 394,
            PlaceholderText = "Ej. Proyecto Expansion Linea 2"
        };

        var buttonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12)
        };

        var createButton = new Button
        {
            Text = "Crear proyecto",
            AutoSize = true,
            BackColor = Color.FromArgb(18, 103, 177),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Padding = new Padding(12, 6, 12, 6),
            DialogResult = DialogResult.None
        };
        createButton.FlatAppearance.BorderSize = 0;
        createButton.Click += CreateButton_Click;

        var cancelButton = new Button
        {
            Text = "Cancelar",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            Padding = new Padding(12, 6, 12, 6),
            DialogResult = DialogResult.Cancel
        };

        buttonsPanel.Controls.Add(createButton);
        buttonsPanel.Controls.Add(cancelButton);

        Controls.Add(buttonsPanel);
        Controls.Add(_projectNameTextBox);
        Controls.Add(helpLabel);
        Controls.Add(titleLabel);

        AcceptButton = createButton;
        CancelButton = cancelButton;
    }

    public string ProjectName => _projectNameTextBox.Text.Trim();

    private void CreateButton_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ProjectName))
        {
            MessageBox.Show(this, "Debes indicar un nombre de proyecto.", "Dato requerido", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _projectNameTextBox.Focus();
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}