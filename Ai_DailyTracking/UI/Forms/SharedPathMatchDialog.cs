using Ai_DailyTracking.Infrastructure;

namespace Ai_DailyTracking.UI.Forms;

public sealed class SharedPathMatchDialog : Form
{
    private readonly ListBox _paths = new() { Dock = DockStyle.Fill, HorizontalScrollbar = true };
    private readonly IReadOnlyList<SharedPathMatch> _matches;

    public SharedPathMatch? SelectedMatch => _paths.SelectedIndex >= 0 ? _matches[_paths.SelectedIndex] : null;

    public SharedPathMatchDialog(IReadOnlyList<SharedPathMatch> matches)
    {
        _matches = matches;
        Text = "Elegir ruta encontrada";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(820, 350);
        MinimumSize = new Size(600, 280);
        Font = new Font("Segoe UI", 10F);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Padding = new Padding(16) };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "Hay varias carpetas que coinciden con la ruta guardada. Elige la correcta."
        }, 0, 0);
        foreach (var match in matches) _paths.Items.Add(match.ResolvedPath);
        if (_paths.Items.Count > 0) _paths.SelectedIndex = 0;
        layout.Controls.Add(_paths, 0, 1);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        var accept = new Button { Text = "Usar esta ruta", AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancelar", AutoSize = true, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(accept);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons, 0, 2);
        AcceptButton = accept;
        CancelButton = cancel;
        Controls.Add(layout);
    }
}
