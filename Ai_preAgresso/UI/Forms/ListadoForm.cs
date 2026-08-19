using System.Globalization;
using Ai_preAgresso.Application.Services;
using Ai_preAgresso.Domain.Models;
using Ai_preAgresso.Infrastructure;

namespace Ai_preAgresso.UI.Forms;

public sealed class ListadoForm : Form
{
    private static readonly string[] TipoFiltroOptions = { "Todos", "Normal", "Fiesta", "Vacaciones", "Baja" };

    private readonly AgressoWorkspaceService _workspaceService;
    private readonly TimeEntryExcelService _excelService;
    private List<TimeEntry> _allEntries = new();

    private readonly TextBox _proyectoFilter = new() { Width = 200 };
    private readonly TextBox _actividadFilter = new() { Width = 160 };
    private readonly TextBox _descripcionFilter = new() { Width = 240 };
    private readonly ComboBox _tipoFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly Button _exportButton = new() { Text = "Exportar filtro a Excel" };
    private readonly Label _totalLabel = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold) };

    private readonly DataGridView _grid = new();
    private readonly DataGridViewTextBoxColumn _fechaColumn = new();
    private readonly DataGridViewTextBoxColumn _semanaColumn = new();
    private readonly DataGridViewTextBoxColumn _diaColumn = new();
    private readonly DataGridViewTextBoxColumn _proyectoColumn = new();
    private readonly DataGridViewTextBoxColumn _actividadColumn = new();
    private readonly DataGridViewTextBoxColumn _descripcionColumn = new();
    private readonly DataGridViewTextBoxColumn _horasColumn = new();
    private readonly DataGridViewTextBoxColumn _tipoColumn = new();

    public ListadoForm(AgressoWorkspaceService workspaceService, TimeEntryExcelService excelService)
    {
        _workspaceService = workspaceService;
        _excelService = excelService;

        Text = "Listado completo - Agresso";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1100, 650);
        Size = new Size(1200, 700);
        Font = new Font("Segoe UI", 9.5F);

        BuildLayout();
        WireEvents();

        _tipoFilter.Items.AddRange(TipoFiltroOptions);
        _tipoFilter.SelectedIndex = 0;

        LoadEntries();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(CreateFilterPanel(), 0, 0);
        root.Controls.Add(CreateGridPanel(), 0, 1);
        root.Controls.Add(CreateFooterPanel(), 0, 2);

        Controls.Add(root);
    }

    private Control CreateFilterPanel()
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(16, 12, 16, 8), WrapContents = false };

        Label MakeLabel(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(0, 8, 6, 0) };

        _proyectoFilter.Margin = new Padding(0, 4, 16, 0);
        _actividadFilter.Margin = new Padding(0, 4, 16, 0);
        _descripcionFilter.Margin = new Padding(0, 4, 16, 0);
        _tipoFilter.Margin = new Padding(0, 4, 0, 0);

        panel.Controls.Add(MakeLabel("Proyecto:"));
        panel.Controls.Add(_proyectoFilter);
        panel.Controls.Add(MakeLabel("Actividad:"));
        panel.Controls.Add(_actividadFilter);
        panel.Controls.Add(MakeLabel("Descripción:"));
        panel.Controls.Add(_descripcionFilter);
        panel.Controls.Add(MakeLabel("Tipo:"));
        panel.Controls.Add(_tipoFilter);

        return panel;
    }

    private Control CreateGridPanel()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.AutoGenerateColumns = false;
        _grid.AllowUserToAddRows = false;
        _grid.ReadOnly = true;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

        _fechaColumn.HeaderText = "Fecha";
        _fechaColumn.Width = 90;
        _semanaColumn.HeaderText = "Año/Semana";
        _semanaColumn.Width = 90;
        _diaColumn.HeaderText = "Día";
        _diaColumn.Width = 60;
        _proyectoColumn.HeaderText = "Proyecto";
        _proyectoColumn.Width = 220;
        _actividadColumn.HeaderText = "Actividad";
        _actividadColumn.Width = 150;
        _descripcionColumn.HeaderText = "Descripción";
        _descripcionColumn.Width = 320;
        _horasColumn.HeaderText = "Horas";
        _horasColumn.Width = 70;
        _tipoColumn.HeaderText = "Tipo";
        _tipoColumn.Width = 100;

        _grid.Columns.AddRange(_fechaColumn, _semanaColumn, _diaColumn, _proyectoColumn, _actividadColumn, _descripcionColumn, _horasColumn, _tipoColumn);

        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 0, 16, 0) };
        panel.Controls.Add(_grid);
        return panel;
    }

    private Control CreateFooterPanel()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(16, 8, 16, 16) };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        _exportButton.Padding = new Padding(8, 4, 8, 4);
        _totalLabel.Anchor = AnchorStyles.Right;

        panel.Controls.Add(_exportButton, 0, 0);
        panel.Controls.Add(_totalLabel, 1, 0);

        return panel;
    }

    private void WireEvents()
    {
        _proyectoFilter.TextChanged += (_, _) => ApplyFilters();
        _actividadFilter.TextChanged += (_, _) => ApplyFilters();
        _descripcionFilter.TextChanged += (_, _) => ApplyFilters();
        _tipoFilter.SelectedIndexChanged += (_, _) => ApplyFilters();
        _exportButton.Click += ExportButton_Click;

        _proyectoFilter.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        _proyectoFilter.AutoCompleteSource = AutoCompleteSource.CustomSource;
        _proyectoFilter.AutoCompleteCustomSource = _workspaceService.AutoComplete.Proyectos;
        _actividadFilter.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        _actividadFilter.AutoCompleteSource = AutoCompleteSource.CustomSource;
        _actividadFilter.AutoCompleteCustomSource = _workspaceService.AutoComplete.Actividades;
        _descripcionFilter.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        _descripcionFilter.AutoCompleteSource = AutoCompleteSource.CustomSource;
        _descripcionFilter.AutoCompleteCustomSource = _workspaceService.AutoComplete.Descripciones;
    }

    private void LoadEntries()
    {
        _allEntries = _workspaceService.GetAllEntries();
        ApplyFilters();
    }

    private List<TimeEntry> GetFilteredEntries()
    {
        var proyecto = _proyectoFilter.Text.Trim();
        var actividad = _actividadFilter.Text.Trim();
        var descripcion = _descripcionFilter.Text.Trim();
        var tipoFiltro = _tipoFilter.SelectedItem as string ?? "Todos";

        return _allEntries
            .Where(entry => proyecto.Length == 0 || entry.Proyecto.Contains(proyecto, StringComparison.OrdinalIgnoreCase))
            .Where(entry => actividad.Length == 0 || entry.Actividad.Contains(actividad, StringComparison.OrdinalIgnoreCase))
            .Where(entry => descripcion.Length == 0 || entry.Descripcion.Contains(descripcion, StringComparison.OrdinalIgnoreCase))
            .Where(entry => tipoFiltro == "Todos" || entry.Tipo.ToString() == tipoFiltro)
            .OrderBy(entry => entry.Fecha)
            .ToList();
    }

    private void ApplyFilters()
    {
        var filtered = GetFilteredEntries();

        _grid.Rows.Clear();
        foreach (var entry in filtered)
        {
            var rowIndex = _grid.Rows.Add();
            var row = _grid.Rows[rowIndex];
            row.Cells[_fechaColumn.Index].Value = entry.Fecha.ToString("dd/MM/yyyy");
            row.Cells[_semanaColumn.Index].Value = $"{WeekPeriodCalculator.GetIsoYear(entry.Fecha)}-S{WeekPeriodCalculator.GetIsoWeek(entry.Fecha):00}";
            row.Cells[_diaColumn.Index].Value = WeekPeriodCalculator.FormatDiaCorto(entry.Fecha);
            row.Cells[_proyectoColumn.Index].Value = entry.Proyecto;
            row.Cells[_actividadColumn.Index].Value = entry.Actividad;
            row.Cells[_descripcionColumn.Index].Value = entry.Descripcion;
            row.Cells[_horasColumn.Index].Value = entry.Horas.ToString("0.##", CultureInfo.CurrentCulture);
            row.Cells[_tipoColumn.Index].Value = entry.Tipo.ToString();
            row.DefaultCellStyle.BackColor = GetRowColor(entry.Tipo);
        }

        _totalLabel.Text = $"Total horas (filtro): {filtered.Sum(entry => entry.Horas):0.##} h  ·  {filtered.Count} filas";
    }

    private static Color GetRowColor(TipoJornada tipo) => tipo switch
    {
        TipoJornada.Fiesta => Color.FromArgb(255, 224, 178),
        TipoJornada.Vacaciones => Color.FromArgb(179, 229, 252),
        TipoJornada.Baja => Color.FromArgb(255, 205, 210),
        _ => Color.White
    };

    private void ExportButton_Click(object? sender, EventArgs e)
    {
        var filtered = GetFilteredEntries();

        using var dialog = new SaveFileDialog { Filter = "Excel (*.xlsx)|*.xlsx", FileName = "Agresso.xlsx" };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            _excelService.Export(filtered, dialog.FileName);
            MessageBox.Show(this, "Exportación completada.", "Agresso", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"No se pudo exportar el archivo:\n{ex.Message}", "Error de exportación", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
