using System.Globalization;
using Ai_preAgresso.Application.Services;
using Ai_preAgresso.Domain.Models;
using Ai_preAgresso.Infrastructure;

namespace Ai_preAgresso.UI.Forms;

public sealed class MainForm : Form
{
    private static readonly string[] TipoDisplayNames = { "Normal", "Fiesta", "Vacaciones", "Baja" };

    private readonly AgressoWorkspaceService _workspaceService;
    private readonly TimeEntryExcelService _excelService;

    private readonly NumericUpDown _yearUpDown = new() { Minimum = 2000, Maximum = 2100, Width = 70 };
    private readonly ComboBox _monthCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    private readonly NumericUpDown _weekUpDown = new() { Minimum = 1, Maximum = 53, Width = 60 };
    private readonly Button _pickWeekButton = new() { Text = "Elegir semana (calendario)" };
    private readonly Button _currentWeekButton = new() { Text = "Semana actual" };
    private readonly Label _dateRangeLabel = new() { AutoSize = true };

    private readonly DataGridView _weekGrid = new();
    private readonly DataGridViewComboBoxColumn _diaColumn = new();
    private readonly DataGridViewTextBoxColumn _proyectoColumn = new();
    private readonly DataGridViewTextBoxColumn _actividadColumn = new();
    private readonly DataGridViewTextBoxColumn _descripcionColumn = new();
    private readonly DataGridViewTextBoxColumn _horasColumn = new();
    private readonly DataGridViewComboBoxColumn _tipoColumn = new();

    private readonly Button _addRowButton = new() { Text = "Añadir fila" };
    private readonly Button _removeRowButton = new() { Text = "Eliminar fila" };
    private readonly Button _saveButton = new() { Text = "Guardar semana" };
    private readonly Button _viewListButton = new() { Text = "Ver listado completo" };
    private readonly Button _importButton = new() { Text = "Importar Excel" };
    private readonly Label _totalHorasLabel = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold) };
    private readonly Label _statusLabel = new() { AutoSize = true, ForeColor = Color.Gray };

    private readonly Dictionary<string, DateOnly> _dayOptionsByDisplay = new(StringComparer.Ordinal);
    private bool _suppressWeekReload;

    public MainForm(AgressoWorkspaceService workspaceService, TimeEntryExcelService excelService)
    {
        _workspaceService = workspaceService;
        _excelService = excelService;

        Text = "Agresso - Registro semanal de tareas";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1200, 720);
        Size = new Size(1300, 820);
        Font = new Font("Segoe UI", 9.5F);

        BuildLayout();
        WireEvents();

        _monthCombo.Items.AddRange(WeekPeriodCalculator.NombresMeses.ToArray());

        var today = DateOnly.FromDateTime(DateTime.Today);
        SetWeek(WeekPeriodCalculator.GetIsoYear(today), WeekPeriodCalculator.GetIsoWeek(today));
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(CreateWeekSelectorPanel(), 0, 0);
        root.Controls.Add(CreateGridPanel(), 0, 1);
        root.Controls.Add(CreateFooterPanel(), 0, 2);

        Controls.Add(root);
    }

    private Control CreateWeekSelectorPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(16, 12, 16, 12),
            BackColor = Color.FromArgb(22, 58, 92),
            WrapContents = false
        };

        Label MakeLabel(string text) => new()
        {
            Text = text,
            ForeColor = Color.White,
            AutoSize = true,
            Margin = new Padding(0, 8, 6, 0)
        };

        _yearUpDown.Margin = new Padding(0, 4, 16, 0);
        _monthCombo.Margin = new Padding(0, 4, 16, 0);
        _weekUpDown.Margin = new Padding(0, 4, 8, 0);
        _pickWeekButton.Margin = new Padding(0, 2, 8, 0);
        _pickWeekButton.Padding = new Padding(6, 4, 6, 4);
        _currentWeekButton.Margin = new Padding(0, 2, 16, 0);
        _currentWeekButton.Padding = new Padding(6, 4, 6, 4);
        _dateRangeLabel.ForeColor = Color.White;
        _dateRangeLabel.Margin = new Padding(0, 8, 0, 0);

        panel.Controls.Add(MakeLabel("Año:"));
        panel.Controls.Add(_yearUpDown);
        panel.Controls.Add(MakeLabel("Mes:"));
        panel.Controls.Add(_monthCombo);
        panel.Controls.Add(MakeLabel("Semana:"));
        panel.Controls.Add(_weekUpDown);
        panel.Controls.Add(_pickWeekButton);
        panel.Controls.Add(_currentWeekButton);
        panel.Controls.Add(_dateRangeLabel);

        return panel;
    }

    private Control CreateGridPanel()
    {
        _weekGrid.Dock = DockStyle.Fill;
        _weekGrid.AutoGenerateColumns = false;
        _weekGrid.AllowUserToAddRows = false;
        _weekGrid.AllowUserToResizeRows = false;
        _weekGrid.RowHeadersVisible = false;
        _weekGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _weekGrid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;

        _diaColumn.HeaderText = "Día";
        _diaColumn.Name = "Dia";
        _diaColumn.Width = 90;
        _diaColumn.FlatStyle = FlatStyle.Flat;

        _proyectoColumn.HeaderText = "Proyecto";
        _proyectoColumn.Name = "Proyecto";
        _proyectoColumn.Width = 220;

        _actividadColumn.HeaderText = "Actividad";
        _actividadColumn.Name = "Actividad";
        _actividadColumn.Width = 150;

        _descripcionColumn.HeaderText = "Descripción";
        _descripcionColumn.Name = "Descripcion";
        _descripcionColumn.Width = 300;

        _horasColumn.HeaderText = "Horas";
        _horasColumn.Name = "Horas";
        _horasColumn.Width = 70;

        _tipoColumn.HeaderText = "Tipo";
        _tipoColumn.Name = "Tipo";
        _tipoColumn.Width = 110;
        _tipoColumn.FlatStyle = FlatStyle.Flat;
        _tipoColumn.Items.AddRange(TipoDisplayNames);

        _weekGrid.Columns.AddRange(_diaColumn, _proyectoColumn, _actividadColumn, _descripcionColumn, _horasColumn, _tipoColumn);

        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 12, 16, 0) };
        panel.Controls.Add(_weekGrid);
        return panel;
    }

    private Control CreateFooterPanel()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(16, 8, 16, 16) };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        var buttonsFlow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        buttonsFlow.Controls.Add(_addRowButton);
        buttonsFlow.Controls.Add(_removeRowButton);
        buttonsFlow.Controls.Add(_saveButton);
        buttonsFlow.Controls.Add(_viewListButton);
        buttonsFlow.Controls.Add(_importButton);
        foreach (Control button in buttonsFlow.Controls)
        {
            button.Margin = new Padding(0, 0, 8, 0);
            button.Padding = new Padding(8, 4, 8, 4);
        }

        var statusFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Anchor = AnchorStyles.Right };
        statusFlow.Controls.Add(_totalHorasLabel);
        statusFlow.Controls.Add(_statusLabel);

        panel.Controls.Add(buttonsFlow, 0, 0);
        panel.Controls.Add(statusFlow, 1, 0);

        return panel;
    }

    private void WireEvents()
    {
        _yearUpDown.ValueChanged += (_, _) => ReloadIfNotSuppressed();
        _weekUpDown.ValueChanged += (_, _) => ReloadIfNotSuppressed();
        _currentWeekButton.Click += (_, _) =>
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            SetWeek(WeekPeriodCalculator.GetIsoYear(today), WeekPeriodCalculator.GetIsoWeek(today));
        };
        _pickWeekButton.Click += PickWeekButton_Click;
        _addRowButton.Click += (_, _) => AddRowButton_Click();
        _removeRowButton.Click += RemoveRowButton_Click;
        _saveButton.Click += (_, _) => SaveWeek();
        _viewListButton.Click += (_, _) => OpenListado();
        _importButton.Click += (_, _) => ImportFromExcel();

        _weekGrid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_weekGrid.IsCurrentCellDirty)
            {
                _weekGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
        _weekGrid.CellValueChanged += (_, e) =>
        {
            if (e.RowIndex >= 0)
            {
                UpdateTotalHoras();
            }
        };
        _weekGrid.RowPrePaint += WeekGrid_RowPrePaint;
        _weekGrid.EditingControlShowing += WeekGrid_EditingControlShowing;
    }

    private void ReloadIfNotSuppressed()
    {
        if (_suppressWeekReload)
        {
            return;
        }
        LoadWeek((int)_yearUpDown.Value, (int)_weekUpDown.Value);
    }

    private void SetWeek(int isoYear, int isoWeek)
    {
        _suppressWeekReload = true;
        _yearUpDown.Value = Math.Clamp(isoYear, (int)_yearUpDown.Minimum, (int)_yearUpDown.Maximum);
        _weekUpDown.Value = Math.Clamp(isoWeek, (int)_weekUpDown.Minimum, (int)_weekUpDown.Maximum);
        _suppressWeekReload = false;
        LoadWeek((int)_yearUpDown.Value, (int)_weekUpDown.Value);
    }

    private void PickWeekButton_Click(object? sender, EventArgs e)
    {
        var currentMonday = WeekPeriodCalculator.GetMonday((int)_yearUpDown.Value, (int)_weekUpDown.Value);
        using var popup = new WeekPickerPopup(currentMonday);
        if (popup.ShowDialog(this) == DialogResult.OK)
        {
            SetWeek(
                WeekPeriodCalculator.GetIsoYear(popup.SelectedMonday),
                WeekPeriodCalculator.GetIsoWeek(popup.SelectedMonday));
        }
    }

    private void LoadWeek(int isoYear, int isoWeek)
    {
        var weekdays = WeekPeriodCalculator.GetWeekdays(isoYear, isoWeek);

        _dayOptionsByDisplay.Clear();
        _diaColumn.Items.Clear();
        foreach (var day in weekdays)
        {
            var display = WeekPeriodCalculator.FormatDiaCorto(day);
            _dayOptionsByDisplay[display] = day;
            _diaColumn.Items.Add(display);
        }

        _monthCombo.SelectedIndex = weekdays[0].Month - 1;
        _dateRangeLabel.Text = $"{weekdays[0]:dd/MM/yyyy} - {weekdays[^1]:dd/MM/yyyy}";

        var entries = _workspaceService.GetEntriesForWeek(isoYear, isoWeek).OrderBy(entry => entry.Fecha).ToList();

        _weekGrid.Rows.Clear();
        foreach (var entry in entries)
        {
            AddGridRow(entry, entry.Fecha);
        }

        // Guarantee a baseline row for every weekday, even if nothing has been entered yet.
        foreach (var day in weekdays)
        {
            if (!entries.Any(entry => entry.Fecha == day))
            {
                AddGridRow(null, day);
            }
        }

        UpdateTotalHoras();
    }

    private void AddRowButton_Click()
    {
        var defaultDate = _dayOptionsByDisplay.Count > 0
            ? _dayOptionsByDisplay.Values.Min()
            : DateOnly.FromDateTime(DateTime.Today);
        AddGridRow(null, defaultDate);
    }

    private void AddGridRow(TimeEntry? entry, DateOnly date)
    {
        var index = _weekGrid.Rows.Add();
        var row = _weekGrid.Rows[index];
        row.Tag = entry?.Id;
        row.Cells[_diaColumn.Index].Value = WeekPeriodCalculator.FormatDiaCorto(date);
        row.Cells[_proyectoColumn.Index].Value = entry?.Proyecto ?? string.Empty;
        row.Cells[_actividadColumn.Index].Value = entry?.Actividad ?? string.Empty;
        row.Cells[_descripcionColumn.Index].Value = entry?.Descripcion ?? string.Empty;
        row.Cells[_horasColumn.Index].Value = entry is null ? string.Empty : entry.Horas.ToString("0.##", CultureInfo.CurrentCulture);
        row.Cells[_tipoColumn.Index].Value = TipoDisplayNames[(int)(entry?.Tipo ?? TipoJornada.Normal)];
    }

    private void RemoveRowButton_Click(object? sender, EventArgs e)
    {
        foreach (DataGridViewRow row in _weekGrid.SelectedRows.Cast<DataGridViewRow>().ToList())
        {
            _weekGrid.Rows.Remove(row);
        }
        UpdateTotalHoras();
    }

    private void WeekGrid_RowPrePaint(object? sender, DataGridViewRowPrePaintEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _weekGrid.Rows.Count)
        {
            return;
        }
        var row = _weekGrid.Rows[e.RowIndex];
        var tipoText = row.Cells[_tipoColumn.Index].Value as string;
        row.DefaultCellStyle.BackColor = GetRowColor(tipoText);
    }

    private static Color GetRowColor(string? tipoText) => tipoText switch
    {
        "Fiesta" => Color.FromArgb(255, 224, 178),
        "Vacaciones" => Color.FromArgb(179, 229, 252),
        "Baja" => Color.FromArgb(255, 205, 210),
        _ => Color.White
    };

    // Attaches history-based suggestions to Proyecto/Actividad/Descripcion cell editors.
    private void WeekGrid_EditingControlShowing(object? sender, DataGridViewEditingControlShowingEventArgs e)
    {
        var columnIndex = _weekGrid.CurrentCell?.ColumnIndex ?? -1;
        if (e.Control is not TextBox textBox)
        {
            return;
        }

        var source = columnIndex == _proyectoColumn.Index ? _workspaceService.AutoComplete.Proyectos
            : columnIndex == _actividadColumn.Index ? _workspaceService.AutoComplete.Actividades
            : columnIndex == _descripcionColumn.Index ? _workspaceService.AutoComplete.Descripciones
            : null;

        if (source is null)
        {
            textBox.AutoCompleteMode = AutoCompleteMode.None;
            return;
        }

        textBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        textBox.AutoCompleteSource = AutoCompleteSource.CustomSource;
        textBox.AutoCompleteCustomSource = source;
    }

    private void UpdateTotalHoras()
    {
        double total = 0;
        foreach (DataGridViewRow row in _weekGrid.Rows)
        {
            if (TryParseHoras(row.Cells[_horasColumn.Index].Value, out var horas))
            {
                total += horas;
            }
        }
        _totalHorasLabel.Text = $"Total semana: {total:0.##} h";
        _totalHorasLabel.ForeColor = Color.FromArgb(22, 58, 92);
    }

    private static bool TryParseHoras(object? value, out double horas)
    {
        horas = 0;
        if (value is null)
        {
            return false;
        }
        var text = value.ToString();
        return double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out horas)
            || double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out horas);
    }

    private void SaveWeek()
    {
        var isoYear = (int)_yearUpDown.Value;
        var isoWeek = (int)_weekUpDown.Value;
        var entries = new List<TimeEntry>();

        foreach (DataGridViewRow row in _weekGrid.Rows)
        {
            var diaDisplay = row.Cells[_diaColumn.Index].Value as string;
            var proyecto = (row.Cells[_proyectoColumn.Index].Value as string ?? string.Empty).Trim();
            var actividad = (row.Cells[_actividadColumn.Index].Value as string ?? string.Empty).Trim();
            var descripcion = (row.Cells[_descripcionColumn.Index].Value as string ?? string.Empty).Trim();
            var tipoText = row.Cells[_tipoColumn.Index].Value as string ?? "Normal";
            TryParseHoras(row.Cells[_horasColumn.Index].Value, out var horas);

            var isEmptyRow = proyecto.Length == 0 && actividad.Length == 0 && descripcion.Length == 0 && horas == 0 && tipoText == "Normal";
            if (isEmptyRow || diaDisplay is null || !_dayOptionsByDisplay.TryGetValue(diaDisplay, out var date))
            {
                continue;
            }

            var tipoIndex = Array.IndexOf(TipoDisplayNames, tipoText);
            entries.Add(new TimeEntry
            {
                Id = row.Tag is Guid existingId ? existingId : Guid.NewGuid(),
                Fecha = date,
                Proyecto = proyecto,
                Actividad = actividad,
                Descripcion = descripcion,
                Horas = horas,
                Tipo = (TipoJornada)Math.Max(0, tipoIndex)
            });
        }

        _workspaceService.SaveWeek(isoYear, isoWeek, entries);
        _statusLabel.Text = $"Guardado a las {DateTime.Now:HH:mm:ss}";
        LoadWeek(isoYear, isoWeek);
    }

    private void OpenListado()
    {
        using var form = new ListadoForm(_workspaceService, _excelService);
        form.ShowDialog(this);
    }

    private void ImportFromExcel()
    {
        using var dialog = new OpenFileDialog { Filter = "Excel (*.xlsx)|*.xlsx", Title = "Importar tareas desde Excel" };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var imported = _excelService.Import(dialog.FileName);
            _workspaceService.ImportEntries(imported);
            LoadWeek((int)_yearUpDown.Value, (int)_weekUpDown.Value);
            _statusLabel.Text = $"Importadas {imported.Count} filas desde Excel";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"No se pudo importar el archivo:\n{ex.Message}", "Error de importación", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
