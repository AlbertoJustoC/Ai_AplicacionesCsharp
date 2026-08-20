using System.Globalization;
using Ai_preAgresso.Application.Services;
using Ai_preAgresso.Domain.Models;
using Ai_preAgresso.Infrastructure;
using Ai_preAgresso.UI.Controls;

namespace Ai_preAgresso.UI.Forms;

public sealed class ListadoForm : Form
{
    private static readonly string[] TipoFiltroOptions = { "Todos", "Normal", "Fiesta", "Vacaciones", "Baja" };

    private sealed record MonthOption(int Number, string Name)
    {
        public override string ToString() => Name;
    }

    private enum FilterField { Proyecto, Actividad, Descripcion, Tipo, Anio, Mes, Semana }

    private readonly AgressoWorkspaceService _workspaceService;
    private readonly TimeEntryExcelService _excelService;
    private List<TimeEntry> _allEntries = new();
    private bool _isRefreshingFilters;

    private readonly MultiSelectComboBox _proyectoFilter = new() { Width = 200 };
    private readonly MultiSelectComboBox _actividadFilter = new() { Width = 160 };
    private readonly MultiSelectComboBox _descripcionFilter = new() { Width = 480 };
    private readonly MultiSelectComboBox _tipoFilter = new() { Width = 120 };
    private readonly MultiSelectComboBox _anioFilter = new() { Width = 90 };
    private readonly MultiSelectComboBox _mesFilter = new() { Width = 130 };
    private readonly MultiSelectComboBox _semanaFilter = new() { Width = 90 };
    private readonly Button _currentWeekFilterButton = new() { Text = "Semana actual", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
    private readonly Button _clearFiltersButton = new() { Text = "Limpiar filtros", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
    private readonly Button _exportButton = new() { Text = "Exportar filtro a Excel", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
    private readonly Button _pieChartButton = new() { Text = "Gráfico de horas (quesitos)", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
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
        var container = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 2, Padding = new Padding(16, 12, 16, 8) };
        container.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        container.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var row1 = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 10) };
        row1.Controls.Add(CreateFilterField("Año:", _anioFilter));
        row1.Controls.Add(CreateFilterField("Mes:", _mesFilter));
        row1.Controls.Add(CreateFilterField("Semana:", _semanaFilter));
        row1.Controls.Add(CreateFilterField(string.Empty, _currentWeekFilterButton));
        row1.Controls.Add(CreateFilterField(string.Empty, _clearFiltersButton));

        var row2 = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        row2.Controls.Add(CreateFilterField("Proyecto:", _proyectoFilter));
        row2.Controls.Add(CreateFilterField("Actividad:", _actividadFilter));
        row2.Controls.Add(CreateFilterField("Descripción:", _descripcionFilter));
        row2.Controls.Add(CreateFilterField("Tipo:", _tipoFilter));

        container.Controls.Add(row1, 0, 0);
        container.Controls.Add(row2, 0, 1);

        return container;
    }

    // Stacks a label above its control so every filter row lines up on a shared baseline.
    private static Control CreateFilterField(string labelText, Control control)
    {
        var stack = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, RowCount = 2, Margin = new Padding(0, 0, 16, 0) };
        stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        stack.Controls.Add(new Label { Text = labelText, AutoSize = true, Margin = new Padding(0, 0, 0, 2) }, 0, 0);
        control.Margin = new Padding(0);
        stack.Controls.Add(control, 0, 1);
        return stack;
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
        _proyectoColumn.MinimumWidth = 220;
        _proyectoColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        _proyectoColumn.FillWeight = 100;
        _actividadColumn.HeaderText = "Actividad";
        _actividadColumn.MinimumWidth = 150;
        _actividadColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        _actividadColumn.FillWeight = 80;
        _descripcionColumn.HeaderText = "Descripción";
        _descripcionColumn.MinimumWidth = 320;
        _descripcionColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        _descripcionColumn.FillWeight = 250;
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

        var buttonsFlow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        buttonsFlow.Controls.Add(_exportButton);
        buttonsFlow.Controls.Add(_pieChartButton);
        foreach (Control button in buttonsFlow.Controls)
        {
            button.Margin = new Padding(0, 0, 8, 0);
            button.Padding = new Padding(8, 4, 8, 4);
        }

        _totalLabel.Anchor = AnchorStyles.Right;

        panel.Controls.Add(buttonsFlow, 0, 0);
        panel.Controls.Add(_totalLabel, 1, 0);

        return panel;
    }

    private void WireEvents()
    {
        _proyectoFilter.SelectionChanged += (_, _) => OnFilterChanged();
        _actividadFilter.SelectionChanged += (_, _) => OnFilterChanged();
        _descripcionFilter.SelectionChanged += (_, _) => OnFilterChanged();
        _tipoFilter.SelectionChanged += (_, _) => OnFilterChanged();
        _anioFilter.SelectionChanged += (_, _) => OnFilterChanged();
        _mesFilter.SelectionChanged += (_, _) => OnFilterChanged();
        _semanaFilter.SelectionChanged += (_, _) => OnFilterChanged();
        _exportButton.Click += ExportButton_Click;
        _pieChartButton.Click += (_, _) => ShowPieChart();
        _currentWeekFilterButton.Click += (_, _) => SetCurrentWeekFilter();
        _clearFiltersButton.Click += (_, _) => ClearAllFilters();
    }

    private void LoadEntries()
    {
        _allEntries = _workspaceService.GetAllEntries();
        RefreshFilterOptions();
        ApplyFilters();
    }

    private void OnFilterChanged()
    {
        if (_isRefreshingFilters)
        {
            return;
        }

        RefreshFilterOptions();
        ApplyFilters();
    }

    private void SetCurrentWeekFilter()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var isoYear = WeekPeriodCalculator.GetIsoYear(today);
        var isoWeek = WeekPeriodCalculator.GetIsoWeek(today);
        var mes = new MonthOption(today.Month, WeekPeriodCalculator.GetNombreMes(today.Month));

        _isRefreshingFilters = true;
        try
        {
            _anioFilter.SetCheckedValues(new object[] { isoYear });
            _mesFilter.SetCheckedValues(new object[] { mes });
            _semanaFilter.SetCheckedValues(new object[] { isoWeek });
        }
        finally
        {
            _isRefreshingFilters = false;
        }

        RefreshFilterOptions();
        ApplyFilters();
    }

    private void ClearAllFilters()
    {
        _isRefreshingFilters = true;
        try
        {
            _proyectoFilter.ClearSelection();
            _actividadFilter.ClearSelection();
            _descripcionFilter.ClearSelection();
            _tipoFilter.ClearSelection();
            _anioFilter.ClearSelection();
            _mesFilter.ClearSelection();
            _semanaFilter.ClearSelection();
        }
        finally
        {
            _isRefreshingFilters = false;
        }

        RefreshFilterOptions();
        ApplyFilters();
    }

    // Each combo's options come from entries matching every OTHER active filter, so picking one field narrows the rest.
    private void RefreshFilterOptions()
    {
        _isRefreshingFilters = true;
        try
        {
            RefreshTextComboOptions(_proyectoFilter, FilterEntries(FilterField.Proyecto).Select(entry => entry.Proyecto));
            RefreshTextComboOptions(_actividadFilter, FilterEntries(FilterField.Actividad).Select(entry => entry.Actividad));
            RefreshTextComboOptions(_descripcionFilter, FilterEntries(FilterField.Descripcion).Select(entry => entry.Descripcion));
            RefreshTipoOptions();
            RefreshAnioOptions();
            RefreshMesOptions();
            RefreshSemanaOptions();
        }
        finally
        {
            _isRefreshingFilters = false;
        }
    }

    private static void RefreshTextComboOptions(MultiSelectComboBox combo, IEnumerable<string> values)
    {
        var distinctValues = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Cast<object>()
            .ToArray();

        combo.SetItems(distinctValues);
    }

    private void RefreshTipoOptions()
    {
        var disponibles = FilterEntries(FilterField.Tipo).Select(entry => entry.Tipo.ToString()).Distinct().ToArray();
        var items = TipoFiltroOptions.Skip(1).Where(disponibles.Contains).Cast<object>().ToArray();
        _tipoFilter.SetItems(items);
    }

    private void RefreshAnioOptions()
    {
        var anios = FilterEntries(FilterField.Anio).Select(entry => WeekPeriodCalculator.GetIsoYear(entry.Fecha)).Distinct().OrderBy(anio => anio).Cast<object>().ToArray();
        _anioFilter.SetItems(anios);
    }

    private void RefreshMesOptions()
    {
        var mesesPresentes = FilterEntries(FilterField.Mes).Select(entry => entry.Fecha.Month).ToHashSet();
        var items = new List<object>();
        for (var mes = 1; mes <= 12; mes++)
        {
            if (mesesPresentes.Contains(mes))
            {
                items.Add(new MonthOption(mes, WeekPeriodCalculator.GetNombreMes(mes)));
            }
        }
        _mesFilter.SetItems(items);
    }

    private void RefreshSemanaOptions()
    {
        var semanas = FilterEntries(FilterField.Semana).Select(entry => WeekPeriodCalculator.GetIsoWeek(entry.Fecha)).Distinct().OrderBy(semana => semana).Cast<object>().ToArray();
        _semanaFilter.SetItems(semanas);
    }

    private List<TimeEntry> GetFilteredEntries() => FilterEntries(exclude: null);

    private List<TimeEntry> FilterEntries(FilterField? exclude)
    {
        var proyectos = _proyectoFilter.CheckedItems.Cast<string>().ToList();
        var actividades = _actividadFilter.CheckedItems.Cast<string>().ToList();
        var descripciones = _descripcionFilter.CheckedItems.Cast<string>().ToList();
        var tipos = _tipoFilter.CheckedItems.Cast<string>().ToList();
        var anios = _anioFilter.CheckedItems.Cast<int>().ToList();
        var meses = _mesFilter.CheckedItems.Cast<MonthOption>().Select(mes => mes.Number).ToList();
        var semanas = _semanaFilter.CheckedItems.Cast<int>().ToList();

        IEnumerable<TimeEntry> query = _allEntries;

        if (exclude != FilterField.Proyecto && proyectos.Count > 0)
        {
            query = query.Where(entry => proyectos.Contains(entry.Proyecto, StringComparer.OrdinalIgnoreCase));
        }
        if (exclude != FilterField.Actividad && actividades.Count > 0)
        {
            query = query.Where(entry => actividades.Contains(entry.Actividad, StringComparer.OrdinalIgnoreCase));
        }
        if (exclude != FilterField.Descripcion && descripciones.Count > 0)
        {
            query = query.Where(entry => descripciones.Contains(entry.Descripcion, StringComparer.OrdinalIgnoreCase));
        }
        if (exclude != FilterField.Tipo && tipos.Count > 0)
        {
            query = query.Where(entry => tipos.Contains(entry.Tipo.ToString()));
        }
        if (exclude != FilterField.Anio && anios.Count > 0)
        {
            query = query.Where(entry => anios.Contains(WeekPeriodCalculator.GetIsoYear(entry.Fecha)));
        }
        if (exclude != FilterField.Mes && meses.Count > 0)
        {
            query = query.Where(entry => meses.Contains(entry.Fecha.Month));
        }
        if (exclude != FilterField.Semana && semanas.Count > 0)
        {
            query = query.Where(entry => semanas.Contains(WeekPeriodCalculator.GetIsoWeek(entry.Fecha)));
        }

        return query.OrderBy(entry => entry.Fecha).ToList();
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

    private void ShowPieChart()
    {
        using var form = new HorasPieChartForm(GetFilteredEntries());
        form.ShowDialog(this);
    }
}
