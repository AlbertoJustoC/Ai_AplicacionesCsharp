using Ai_preAgresso.Domain.Models;
using Ai_preAgresso.UI.Controls;

namespace Ai_preAgresso.UI.Forms;

public sealed class HorasPieChartForm : Form
{
    private static readonly string[] AgrupacionOptions = { "Proyecto", "Actividad", "Tipo" };

    private readonly List<TimeEntry> _entries;
    private readonly ComboBox _agruparPorCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    private readonly PieChartPanel _chartPanel = new() { Dock = DockStyle.Fill };

    public HorasPieChartForm(List<TimeEntry> entries)
    {
        _entries = entries;

        Text = "Gráfico de horas (quesitos)";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(760, 520);
        Size = new Size(820, 560);
        Font = new Font("Segoe UI", 9.5F);

        BuildLayout();

        _agruparPorCombo.Items.AddRange(AgrupacionOptions);
        _agruparPorCombo.SelectedIndexChanged += (_, _) => RenderChart();
        _agruparPorCombo.SelectedIndex = 0;
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var header = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(16, 12, 16, 8), WrapContents = false };
        header.Controls.Add(new Label { Text = "Agrupar por:", AutoSize = true, Margin = new Padding(0, 6, 8, 0) });
        header.Controls.Add(_agruparPorCombo);

        var chartHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 0, 16, 16) };
        chartHost.Controls.Add(_chartPanel);

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(chartHost, 0, 1);
        Controls.Add(root);
    }

    private void RenderChart()
    {
        var agrupacion = _agruparPorCombo.SelectedItem as string ?? "Proyecto";

        var slices = agrupacion switch
        {
            "Actividad" => GroupBy(entry => string.IsNullOrWhiteSpace(entry.Actividad) ? "(Sin actividad)" : entry.Actividad),
            "Tipo" => GroupBy(entry => entry.Tipo.ToString()),
            _ => GroupBy(entry => string.IsNullOrWhiteSpace(entry.Proyecto) ? "(Sin proyecto)" : entry.Proyecto)
        };

        _chartPanel.SetData($"Horas por {agrupacion.ToLowerInvariant()} ({_entries.Sum(entry => entry.Horas):0.##} h totales)", slices);
    }

    private List<(string Label, double Value)> GroupBy(Func<TimeEntry, string> keySelector)
    {
        return _entries
            .GroupBy(keySelector)
            .Select(group => (Label: group.Key, Value: group.Sum(entry => entry.Horas)))
            .Where(slice => slice.Value > 0)
            .OrderByDescending(slice => slice.Value)
            .ToList();
    }
}
