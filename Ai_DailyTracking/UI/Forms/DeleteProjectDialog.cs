using Ai_DailyTracking.Domain.Models;

namespace Ai_DailyTracking.UI.Forms;

// Lets the user pick a project to delete permanently; deletion happens right on this dialog with no extra warning popup.
public sealed class DeleteProjectDialog : Form
{
    private readonly ListBox _projectsListBox;
    private readonly Button _deleteButton;

    public DeleteProjectDialog(IEnumerable<TrackingProject> projects)
    {
        Text = "Eliminar proyecto";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(440, 360);
        BackColor = Color.FromArgb(245, 247, 250);
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);

        var titleLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 44,
            Text = "Elige el proyecto a eliminar",
            Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold, GraphicsUnit.Point),
            Padding = new Padding(20, 14, 20, 0)
        };

        var helpLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 34,
            Text = "Se borrara el archivo del proyecto y todos sus registros de forma permanente.",
            ForeColor = Color.FromArgb(88, 96, 110),
            Padding = new Padding(20, 0, 20, 0)
        };

        _projectsListBox = new ListBox
        {
            Left = 20,
            Top = 90,
            Width = 394,
            Height = 190
        };

        foreach (var project in projects)
        {
            _projectsListBox.Items.Add(new ProjectListItem(project.ProjectId, project.ProjectName));
        }

        var buttonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12)
        };

        _deleteButton = new Button
        {
            Text = "Eliminar proyecto",
            AutoSize = true,
            BackColor = Color.FromArgb(178, 34, 52),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Padding = new Padding(12, 6, 12, 6),
            Enabled = false
        };
        _deleteButton.FlatAppearance.BorderSize = 0;
        _deleteButton.Click += DeleteButton_Click;

        _projectsListBox.SelectedIndexChanged += (_, _) => _deleteButton.Enabled = _projectsListBox.SelectedItem is not null;

        var cancelButton = new Button
        {
            Text = "Cancelar",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            Padding = new Padding(12, 6, 12, 6),
            DialogResult = DialogResult.Cancel
        };

        buttonsPanel.Controls.Add(_deleteButton);
        buttonsPanel.Controls.Add(cancelButton);

        Controls.Add(_projectsListBox);
        Controls.Add(buttonsPanel);
        Controls.Add(helpLabel);
        Controls.Add(titleLabel);

        CancelButton = cancelButton;
    }

    public Guid SelectedProjectId { get; private set; }

    private void DeleteButton_Click(object? sender, EventArgs e)
    {
        if (_projectsListBox.SelectedItem is not ProjectListItem item)
        {
            return;
        }

        var confirmation = MessageBox.Show(
            this,
            $"Esta accion no se puede deshacer: se perderan para siempre todos los datos del proyecto \"{item.ProjectName}\". ¿Deseas continuar?",
            "Eliminar proyecto",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (confirmation != DialogResult.Yes)
        {
            return;
        }

        SelectedProjectId = item.ProjectId;
        DialogResult = DialogResult.OK;
        Close();
    }

    private sealed class ProjectListItem
    {
        public ProjectListItem(Guid projectId, string projectName)
        {
            ProjectId = projectId;
            ProjectName = projectName;
        }

        public Guid ProjectId { get; }

        public string ProjectName { get; }

        public override string ToString()
        {
            return ProjectName;
        }
    }
}
