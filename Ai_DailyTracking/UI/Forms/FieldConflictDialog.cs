using Ai_DailyTracking.Domain.Models;

namespace Ai_DailyTracking.UI.Forms;

public sealed class FieldConflictDialog : Form
{
    private readonly ListBox _versions = new();
    private readonly TextBox _selectedValue = new();
    private readonly IReadOnlyList<ProjectFieldCandidate> _candidates;

    public Guid? SelectedChangeId => _versions.SelectedIndex >= 0
        ? _candidates[_versions.SelectedIndex].ChangeId : null;

    public FieldConflictDialog(ProjectFieldConflict conflict)
    {
        _candidates = conflict.Candidates;
        Text = "Conflicto en un registro";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(820, 520);
        MinimumSize = Size;
        Font = new Font("Segoe UI", 10F);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, Padding = new Padding(16) };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 45F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 55F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(700, 0),
            Text = $"El registro #{conflict.EntryNumber} tiene cambios distintos en «{FieldLabel(conflict.FieldKey)}». " +
                   "Elige el valor que debe quedar. La versión más reciente según los relojes de los PC aparece primero."
        }, 0, 0);

        _versions.Dock = DockStyle.Fill;
        _versions.HorizontalScrollbar = true;
        foreach (var candidate in _candidates)
        {
            var time = candidate.SavedAtUtc == DateTimeOffset.MinValue
                ? "Fecha anterior a la migración"
                : candidate.SavedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
            var preview = DisplayValue(conflict.FieldKey, candidate.Value)
                .Replace('\n', ' ').Replace('\r', ' ');
            if (preview.Length > 90) preview = preview[..90] + "…";
            var editor = string.IsNullOrWhiteSpace(candidate.Machine)
                ? candidate.Author : $"{candidate.Author} ({candidate.Machine})";
            _versions.Items.Add($"{time} · {editor} · {preview}");
        }
        _versions.SelectedIndexChanged += (_, _) =>
            _selectedValue.Text = SelectedChangeId is { } id
                ? DisplayValue(conflict.FieldKey, _candidates.First(candidate => candidate.ChangeId == id).Value)
                : string.Empty;
        if (_versions.Items.Count > 0) _versions.SelectedIndex = 0;
        layout.Controls.Add(_versions, 0, 1);

        _selectedValue.Dock = DockStyle.Fill;
        _selectedValue.Multiline = true;
        _selectedValue.ReadOnly = true;
        _selectedValue.ScrollBars = ScrollBars.Vertical;
        layout.Controls.Add(_selectedValue, 0, 2);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        var choose = new Button { Text = "Conservar versión seleccionada", AutoSize = true, DialogResult = DialogResult.OK };
        var later = new Button { Text = "Resolver después", AutoSize = true, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(choose);
        buttons.Controls.Add(later);
        layout.Controls.Add(buttons, 0, 3);
        AcceptButton = choose;
        CancelButton = later;
        Controls.Add(layout);
    }

    private static string FieldLabel(string key) => key switch
    {
        "status" => "Estado",
        "priority" => "Prioridad",
        "activity" => "Actividad",
        "assignedto" => "Asignado a",
        "recorddate" => "Fecha del registro",
        "updateddate" => "Último cambio",
        "__deleted" => "Eliminación del registro",
        _ => key
    };

    private static string DisplayValue(string fieldKey, string? value) => fieldKey == "__deleted"
        ? value == "true" ? "Eliminar registro" : "Conservar registro"
        : value ?? "(vacío)";
}
