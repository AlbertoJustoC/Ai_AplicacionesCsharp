namespace Ai_DailyTracking.UI.Forms;

// Lets the user manage which Windows users/emails are allowed to see a project in the project list.
public sealed class ProjectUsersDialog : Form
{
    private readonly TextBox _userTextBox;
    private readonly ListBox _usersListBox;

    public ProjectUsersDialog(string projectName, IEnumerable<string> currentUsers)
    {
        Text = "Usuarios del proyecto";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(440, 392);
        BackColor = Color.FromArgb(245, 247, 250);
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);

        var titleLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 44,
            Text = $"Usuarios con acceso a \"{projectName}\"",
            Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold, GraphicsUnit.Point),
            Padding = new Padding(20, 14, 20, 0)
        };

        var helpLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 66,
            Text = "El proyecto solo aparecera para quien inicie sesion en Windows con uno de estos nombres. Si escribes un correo, se compara la parte antes de la @ con el usuario de Windows. Si no añades ninguno, el proyecto es visible para todos.",
            ForeColor = Color.FromArgb(88, 96, 110),
            Padding = new Padding(20, 0, 20, 0)
        };

        _userTextBox = new TextBox
        {
            Left = 20,
            Top = 120,
            Width = 300,
            PlaceholderText = "usuario o correo@ejemplo.com"
        };

        var addButton = new Button
        {
            Left = 328,
            Top = 118,
            Width = 92,
            Height = 28,
            Text = "Añadir",
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(18, 103, 177),
            ForeColor = Color.White
        };
        addButton.FlatAppearance.BorderSize = 0;
        addButton.Click += AddButton_Click;

        _usersListBox = new ListBox
        {
            Left = 20,
            Top = 156,
            Width = 394,
            Height = 138
        };

        foreach (var userName in currentUsers)
        {
            if (!string.IsNullOrWhiteSpace(userName))
            {
                _usersListBox.Items.Add(userName.Trim());
            }
        }

        var removeButton = new Button
        {
            Left = 20,
            Top = 302,
            AutoSize = true,
            Text = "Quitar seleccionado",
            FlatStyle = FlatStyle.Flat,
            Padding = new Padding(10, 4, 10, 4)
        };
        removeButton.Click += RemoveButton_Click;

        var buttonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12)
        };

        var saveButton = new Button
        {
            Text = "Guardar",
            AutoSize = true,
            BackColor = Color.FromArgb(18, 103, 177),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Padding = new Padding(12, 6, 12, 6),
            DialogResult = DialogResult.OK
        };
        saveButton.FlatAppearance.BorderSize = 0;

        var cancelButton = new Button
        {
            Text = "Cancelar",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            Padding = new Padding(12, 6, 12, 6),
            DialogResult = DialogResult.Cancel
        };

        buttonsPanel.Controls.Add(saveButton);
        buttonsPanel.Controls.Add(cancelButton);

        Controls.Add(removeButton);
        Controls.Add(_usersListBox);
        Controls.Add(addButton);
        Controls.Add(_userTextBox);
        Controls.Add(buttonsPanel);
        Controls.Add(helpLabel);
        Controls.Add(titleLabel);

        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    public IReadOnlyList<string> Users => _usersListBox.Items.Cast<string>().ToList();

    private void AddButton_Click(object? sender, EventArgs e)
    {
        var value = _userTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!_usersListBox.Items.Cast<string>().Any(existing => string.Equals(existing, value, StringComparison.OrdinalIgnoreCase)))
        {
            _usersListBox.Items.Add(value);
        }

        _userTextBox.Clear();
        _userTextBox.Focus();
    }

    private void RemoveButton_Click(object? sender, EventArgs e)
    {
        if (_usersListBox.SelectedItem is not null)
        {
            _usersListBox.Items.Remove(_usersListBox.SelectedItem);
        }
    }
}
