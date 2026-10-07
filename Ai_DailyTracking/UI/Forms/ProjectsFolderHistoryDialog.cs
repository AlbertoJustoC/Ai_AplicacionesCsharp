namespace Ai_DailyTracking.UI.Forms;

public sealed class ProjectsFolderHistoryDialog : Form
{
    private readonly ListBox _pathsList = new();
    private readonly Action<string> _removePath;
    private readonly string _currentPath;

    public string? SelectedFolderPath { get; private set; }

    public ProjectsFolderHistoryDialog(IReadOnlyList<string> paths, string currentPath, Action<string> removePath)
    {
        _removePath = removePath;
        _currentPath = currentPath;

        Text = "Carpeta de proyectos";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(650, 300);
        Size = new Size(850, 400);
        Font = new Font("Segoe UI", 9.5F);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(16)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(new Label
        {
            Text = "Últimas carpetas de proyectos utilizadas:",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
        }, 0, 0);

        _pathsList.Dock = DockStyle.Fill;
        _pathsList.HorizontalScrollbar = true;
        _pathsList.IntegralHeight = false;
        _pathsList.Items.AddRange(paths.Cast<object>().ToArray());
        _pathsList.DoubleClick += (_, _) => SelectListedPath();
        if (_pathsList.Items.Count > 0)
        {
            _pathsList.SelectedIndex = 0;
        }
        layout.Controls.Add(_pathsList, 0, 1);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 12, 0, 0)
        };

        var cancelButton = new Button { Text = "Cancelar", AutoSize = true, DialogResult = DialogResult.Cancel };
        var browseButton = new Button { Text = "Examinar", AutoSize = true };
        var selectButton = new Button { Text = "Seleccionar", AutoSize = true };
        browseButton.Click += (_, _) => BrowseForPath();
        selectButton.Click += (_, _) => SelectListedPath();
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(browseButton);
        buttons.Controls.Add(selectButton);
        layout.Controls.Add(buttons, 0, 2);

        AcceptButton = selectButton;
        CancelButton = cancelButton;
        Controls.Add(layout);
    }

    private void SelectListedPath()
    {
        if (_pathsList.SelectedItem is not string path)
        {
            return;
        }

        if (!Directory.Exists(path))
        {
            var remove = MessageBox.Show(
                this,
                $"La carpeta ya no existe o no está disponible:\n{path}\n\n¿Quitarla del listado?",
                "Carpeta no disponible",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (remove == DialogResult.Yes)
            {
                _removePath(path);
                _pathsList.Items.Remove(path);
                if (_pathsList.Items.Count > 0)
                {
                    _pathsList.SelectedIndex = 0;
                }
            }

            return;
        }

        SelectedFolderPath = path;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void BrowseForPath()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Selecciona la carpeta compartida donde se guardarán los proyectos",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(_currentPath) ? _currentPath : string.Empty
        };

        if (dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            return;
        }

        SelectedFolderPath = dialog.SelectedPath;
        DialogResult = DialogResult.OK;
        Close();
    }

}
