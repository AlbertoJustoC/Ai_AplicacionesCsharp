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
    private readonly Button _pickWeekButton = new() { Text = "Elegir semana (calendario)", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
    private readonly Button _currentWeekButton = new() { Text = "Semana actual", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
    private readonly Label _dateRangeLabel = new() { AutoSize = true };

    private readonly DataGridView _weekGrid = new();
    private readonly DataGridViewComboBoxColumn _diaColumn = new();
    private readonly DataGridViewTextBoxColumn _proyectoColumn = new();
    private readonly DataGridViewTextBoxColumn _actividadColumn = new();
    private readonly DataGridViewTextBoxColumn _descripcionColumn = new();
    private readonly DataGridViewTextBoxColumn _horasColumn = new();
    private readonly DataGridViewComboBoxColumn _tipoColumn = new();

    private readonly Button _addRowButton = new() { Text = "Añadir fila", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
    private readonly Button _removeRowButton = new() { Text = "Eliminar fila", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
    private readonly Button _saveButton = new() { Text = "Guardar semana", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
    private readonly Button _viewListButton = new() { Text = "Ver listado completo", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
    private readonly Button _changeProjectFileButton = new() { Text = "Archivo de proyecto...", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
    private readonly Label _totalHorasLabel = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold) };
    private readonly Label _statusLabel = new() { AutoSize = true, ForeColor = Color.Gray };
    private readonly Label _projectFileLabel = new() { AutoSize = true, ForeColor = Color.Gray };
    private readonly ToolTip _toolTip = new();

    private readonly Dictionary<string, DateOnly> _dayOptionsByDisplay = new(StringComparer.Ordinal);
    private bool _suppressUiEvents;
    private bool _isLoadingGrid;
    private bool _isDirty;
    private int _currentIsoYear;
    private int _currentIsoWeek;

    public MainForm(AgressoWorkspaceService workspaceService, TimeEntryExcelService excelService)
    {
        _workspaceService = workspaceService;
        _excelService = excelService;

        Text = "Agresso - Registro semanal de tareas";
        Icon = Ai_preAgresso.Shared.Helpers.AppIconProvider.Current;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1200, 720);
        Size = new Size(1300, 820);
        Font = new Font("Segoe UI", 9.5F);

        BuildLayout();
        WireEvents();

        _monthCombo.Items.AddRange(WeekPeriodCalculator.NombresMeses.ToArray());

        var today = DateOnly.FromDateTime(DateTime.Today);
        SetWeek(WeekPeriodCalculator.GetIsoYear(today), WeekPeriodCalculator.GetIsoWeek(today));
        UpdateProjectFileLabel();
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

        // Standard button faces read poorly against the dark navy panel; force a high-contrast look.
        foreach (var navButton in new[] { _pickWeekButton, _currentWeekButton })
        {
            navButton.FlatStyle = FlatStyle.Flat;
            navButton.BackColor = Color.FromArgb(35, 95, 145);
            navButton.ForeColor = Color.White;
            navButton.Font = new Font(Font, FontStyle.Bold);
            navButton.FlatAppearance.BorderColor = Color.White;
        }

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
        _proyectoColumn.MinimumWidth = 220;
        _proyectoColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        _proyectoColumn.FillWeight = 100;

        _actividadColumn.HeaderText = "Actividad";
        _actividadColumn.Name = "Actividad";
        _actividadColumn.Width = 150;

        _descripcionColumn.HeaderText = "Descripción";
        _descripcionColumn.Name = "Descripcion";
        _descripcionColumn.MinimumWidth = 300;
        _descripcionColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        _descripcionColumn.FillWeight = 200;

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
        foreach (Control button in buttonsFlow.Controls)
        {
            button.Margin = new Padding(0, 0, 8, 0);
            button.Padding = new Padding(8, 4, 8, 4);
        }

        // Distinct color/position from the work buttons since it manages the project file, not the current week.
        _changeProjectFileButton.FlatStyle = FlatStyle.Flat;
        _changeProjectFileButton.BackColor = Color.FromArgb(120, 80, 150);
        _changeProjectFileButton.ForeColor = Color.White;
        _changeProjectFileButton.FlatAppearance.BorderColor = Color.FromArgb(90, 60, 115);
        _changeProjectFileButton.Margin = new Padding(0, 0, 0, 6);
        _changeProjectFileButton.Padding = new Padding(8, 4, 8, 4);
        _changeProjectFileButton.Anchor = AnchorStyles.Right;

        var statusFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Anchor = AnchorStyles.Right };
        statusFlow.Controls.Add(_totalHorasLabel);
        statusFlow.Controls.Add(_statusLabel);
        statusFlow.Controls.Add(_projectFileLabel);

        var rightFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Anchor = AnchorStyles.Right, WrapContents = false };
        rightFlow.Controls.Add(_changeProjectFileButton);
        rightFlow.Controls.Add(statusFlow);

        panel.Controls.Add(buttonsFlow, 0, 0);
        panel.Controls.Add(rightFlow, 1, 0);

        return panel;
    }

    private void WireEvents()
    {
        _yearUpDown.ValueChanged += (_, _) => ReloadIfNotSuppressed();
        _weekUpDown.ValueChanged += (_, _) => ReloadIfNotSuppressed();
        _monthCombo.SelectedIndexChanged += (_, _) => MonthCombo_SelectedIndexChanged();
        _currentWeekButton.Click += (_, _) =>
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            RequestWeekChange(WeekPeriodCalculator.GetIsoYear(today), WeekPeriodCalculator.GetIsoWeek(today));
        };
        _pickWeekButton.Click += PickWeekButton_Click;
        _addRowButton.Click += (_, _) => AddRowButton_Click();
        _removeRowButton.Click += RemoveRowButton_Click;
        _saveButton.Click += (_, _) => SaveWeek();
        _viewListButton.Click += (_, _) => OpenListado();
        _changeProjectFileButton.Click += (_, _) => ChangeProjectFile();

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
                if (!_isLoadingGrid)
                {
                    _isDirty = true;
                }
            }
        };
        _weekGrid.RowPrePaint += WeekGrid_RowPrePaint;
        _weekGrid.EditingControlShowing += WeekGrid_EditingControlShowing;
        _weekGrid.CellValidating += WeekGrid_CellValidating;
        _weekGrid.CellEndEdit += WeekGrid_CellEndEdit;
    }

    private void ReloadIfNotSuppressed()
    {
        if (_suppressUiEvents)
        {
            return;
        }

        var isoYear = (int)_yearUpDown.Value;
        var isoWeek = WeekPeriodCalculator.ClampIsoWeek(isoYear, (int)_weekUpDown.Value);
        if (isoWeek != (int)_weekUpDown.Value)
        {
            _suppressUiEvents = true;
            _weekUpDown.Value = isoWeek;
            _suppressUiEvents = false;
        }

        RequestWeekChange(isoYear, isoWeek);
    }

    private void MonthCombo_SelectedIndexChanged()
    {
        if (_suppressUiEvents || _monthCombo.SelectedIndex < 0)
        {
            return;
        }

        var target = new DateOnly((int)_yearUpDown.Value, _monthCombo.SelectedIndex + 1, 1);
        RequestWeekChange(WeekPeriodCalculator.GetIsoYear(target), WeekPeriodCalculator.GetIsoWeek(target));
    }

    // Gatekeeper for every week-navigation entry point: warns about unsaved changes
    // before actually switching, and never saves automatically.
    private void RequestWeekChange(int isoYear, int isoWeek)
    {
        var clampedYear = Math.Clamp(isoYear, (int)_yearUpDown.Minimum, (int)_yearUpDown.Maximum);
        var clampedWeek = WeekPeriodCalculator.ClampIsoWeek(clampedYear, isoWeek);

        if (clampedYear == _currentIsoYear && clampedWeek == _currentIsoWeek)
        {
            return;
        }

        if (_isDirty)
        {
            var choice = MessageBox.Show(
                this,
                "La semana activa tiene cambios sin guardar. ¿Quieres guardarlos antes de cambiar de semana?",
                "Cambios sin guardar",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning);

            if (choice == DialogResult.Cancel)
            {
                RevertWeekSelectorsToCurrent();
                return;
            }

            if (choice == DialogResult.Yes)
            {
                SaveWeek(_currentIsoYear, _currentIsoWeek);
            }
        }

        SetWeek(clampedYear, clampedWeek);
    }

    private void RevertWeekSelectorsToCurrent()
    {
        _suppressUiEvents = true;
        _yearUpDown.Value = _currentIsoYear;
        _weekUpDown.Value = _currentIsoWeek;
        _monthCombo.SelectedIndex = WeekPeriodCalculator.GetMonday(_currentIsoYear, _currentIsoWeek).Month - 1;
        _suppressUiEvents = false;
    }

    private void SetWeek(int isoYear, int isoWeek)
    {
        var clampedYear = Math.Clamp(isoYear, (int)_yearUpDown.Minimum, (int)_yearUpDown.Maximum);
        var clampedWeek = WeekPeriodCalculator.ClampIsoWeek(clampedYear, isoWeek);

        _suppressUiEvents = true;
        _yearUpDown.Value = clampedYear;
        _weekUpDown.Value = clampedWeek;
        _suppressUiEvents = false;

        LoadWeek(clampedYear, clampedWeek);
    }

    private void PickWeekButton_Click(object? sender, EventArgs e)
    {
        var currentMonday = WeekPeriodCalculator.GetMonday((int)_yearUpDown.Value, (int)_weekUpDown.Value);
        using var popup = new WeekPickerPopup(currentMonday);
        if (popup.ShowDialog(this) == DialogResult.OK)
        {
            RequestWeekChange(
                WeekPeriodCalculator.GetIsoYear(popup.SelectedMonday),
                WeekPeriodCalculator.GetIsoWeek(popup.SelectedMonday));
        }
    }

    private void LoadWeek(int isoYear, int isoWeek)
    {
        _isLoadingGrid = true;
        try
        {
            var weekdays = WeekPeriodCalculator.GetWeekdays(isoYear, isoWeek);

            // Rows must be cleared before the Día combo's Items change, otherwise the grid tries to
            // validate old rows against the new (unrelated) week's items and throws.
            _weekGrid.Rows.Clear();

            _dayOptionsByDisplay.Clear();
            _diaColumn.Items.Clear();
            foreach (var day in weekdays)
            {
                var display = WeekPeriodCalculator.FormatDiaCorto(day);
                _dayOptionsByDisplay[display] = day;
                _diaColumn.Items.Add(display);
            }

            _suppressUiEvents = true;
            _monthCombo.SelectedIndex = weekdays[0].Month - 1;
            _suppressUiEvents = false;

            _dateRangeLabel.Text = $"{weekdays[0]:dd/MM/yyyy} - {weekdays[^1]:dd/MM/yyyy}";

            var entries = _workspaceService.GetEntriesForWeek(isoYear, isoWeek).OrderBy(entry => entry.Fecha).ToList();

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
        finally
        {
            _isLoadingGrid = false;
        }

        _currentIsoYear = isoYear;
        _currentIsoWeek = isoWeek;
        _isDirty = false;
    }

    private void AddRowButton_Click()
    {
        var selectedRow = _weekGrid.CurrentRow ?? _weekGrid.SelectedRows.Cast<DataGridViewRow>().FirstOrDefault();
        int insertAt;
        DateOnly date;

        if (selectedRow is not null
            && selectedRow.Cells[_diaColumn.Index].Value is string diaDisplay
            && _dayOptionsByDisplay.TryGetValue(diaDisplay, out var selectedDate))
        {
            insertAt = selectedRow.Index + 1;
            date = selectedDate;
        }
        else
        {
            insertAt = _weekGrid.Rows.Count;
            date = _dayOptionsByDisplay.Count > 0 ? _dayOptionsByDisplay.Values.Min() : DateOnly.FromDateTime(DateTime.Today);
        }

        AddGridRow(null, date, insertAt);
        UpdateTotalHoras();
        _weekGrid.CurrentCell = _weekGrid.Rows[insertAt].Cells[_proyectoColumn.Index];
    }

    private void AddGridRow(TimeEntry? entry, DateOnly date, int? insertAt = null)
    {
        int index;
        if (insertAt.HasValue && insertAt.Value >= 0 && insertAt.Value < _weekGrid.Rows.Count)
        {
            _weekGrid.Rows.Insert(insertAt.Value);
            index = insertAt.Value;
        }
        else
        {
            index = _weekGrid.Rows.Add();
        }
        var row = _weekGrid.Rows[index];
        row.Tag = entry?.Id;
        row.Cells[_diaColumn.Index].Value = WeekPeriodCalculator.FormatDiaCorto(date);
        row.Cells[_diaColumn.Index].Style.Font = new Font(_weekGrid.Font, BoldDiaDays.Contains(date.DayOfWeek) ? FontStyle.Bold : FontStyle.Regular);
        row.Cells[_proyectoColumn.Index].Value = entry?.Proyecto ?? string.Empty;
        row.Cells[_actividadColumn.Index].Value = entry?.Actividad ?? string.Empty;
        row.Cells[_descripcionColumn.Index].Value = entry?.Descripcion ?? string.Empty;
        row.Cells[_horasColumn.Index].Value = entry is null ? string.Empty : entry.Horas.ToString("0.##", CultureInfo.InvariantCulture);
        row.Cells[_tipoColumn.Index].Value = TipoDisplayNames[(int)(entry?.Tipo ?? TipoJornada.Normal)];
    }

    private void RemoveRowButton_Click(object? sender, EventArgs e)
    {
        var rowsToRemove = _weekGrid.SelectedRows.Cast<DataGridViewRow>().ToList();
        if (rowsToRemove.Count == 0)
        {
            return;
        }

        foreach (var row in rowsToRemove)
        {
            _weekGrid.Rows.Remove(row);
        }
        _isDirty = true;
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

    private static readonly DayOfWeek[] BoldDiaDays = { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday };

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

    private void WeekGrid_CellValidating(object? sender, DataGridViewCellValidatingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != _horasColumn.Index)
        {
            return;
        }

        var input = e.FormattedValue?.ToString();
        if (string.IsNullOrWhiteSpace(input))
        {
            _weekGrid.Rows[e.RowIndex].ErrorText = string.Empty;
            return;
        }

        if (!TryParseHoras(input, out _, out _, out var errorMessage))
        {
            e.Cancel = true;
            _weekGrid.Rows[e.RowIndex].ErrorText = errorMessage;
            return;
        }

        _weekGrid.Rows[e.RowIndex].ErrorText = string.Empty;
    }

    private void WeekGrid_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != _horasColumn.Index)
        {
            return;
        }

        var cell = _weekGrid.Rows[e.RowIndex].Cells[e.ColumnIndex];
        if (!TryParseHoras(cell.Value, out _, out var normalized, out _))
        {
            cell.Value = string.Empty;
        }
        else
        {
            cell.Value = normalized;
        }

        _weekGrid.Rows[e.RowIndex].ErrorText = string.Empty;
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
        _totalHorasLabel.Text = $"Total semana: {total.ToString("0.##", CultureInfo.InvariantCulture)} h";
        _totalHorasLabel.ForeColor = Color.FromArgb(22, 58, 92);
    }

    private static bool TryParseHoras(object? value, out double horas)
    {
        return TryParseHoras(value, out horas, out _, out _);
    }

    private static bool TryParseHoras(object? value, out double horas, out string normalized, out string errorMessage)
    {
        horas = 0;
        normalized = string.Empty;
        errorMessage = string.Empty;

        var text = value?.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (!TryParseHorasDecimal(text, out var horasDecimal))
        {
            errorMessage = "Horas debe ser un número válido (usa punto decimal, por ejemplo 1.5).";
            return false;
        }

        horasDecimal = Math.Round(horasDecimal, 2, MidpointRounding.AwayFromZero);

        horas = (double)horasDecimal;
        normalized = horasDecimal.ToString("0.##", CultureInfo.InvariantCulture);
        return true;
    }

    private static bool TryParseHorasDecimal(string text, out decimal horas)
    {
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out horas);
    }

    private void SaveWeek() => SaveWeek((int)_yearUpDown.Value, (int)_weekUpDown.Value);

    private void SaveWeek(int isoYear, int isoWeek)
    {
        var entries = new List<TimeEntry>();

        foreach (DataGridViewRow row in _weekGrid.Rows)
        {
            var diaDisplay = row.Cells[_diaColumn.Index].Value as string;
            var proyecto = (row.Cells[_proyectoColumn.Index].Value as string ?? string.Empty).Trim();
            var actividad = (row.Cells[_actividadColumn.Index].Value as string ?? string.Empty).Trim();
            var descripcion = (row.Cells[_descripcionColumn.Index].Value as string ?? string.Empty).Trim();
            var tipoText = row.Cells[_tipoColumn.Index].Value as string ?? "Normal";
            var rawHoras = row.Cells[_horasColumn.Index].Value;
            var horasText = rawHoras?.ToString()?.Trim() ?? string.Empty;
            if (horasText.Length > 0 && !TryParseHoras(rawHoras, out _, out var normalizedHoras, out var horasError))
            {
                MessageBox.Show(
                    this,
                    $"Fila {row.Index + 1}: {horasError}",
                    "Horas inválidas",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                _weekGrid.CurrentCell = row.Cells[_horasColumn.Index];
                _weekGrid.BeginEdit(true);
                return;
            }

            TryParseHoras(rawHoras, out var horas, out var normalizedHorasForCell, out _);
            row.Cells[_horasColumn.Index].Value = normalizedHorasForCell;

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

    private void ChangeProjectFile()
    {
        if (_isDirty)
        {
            var choice = MessageBox.Show(
                this,
                "La semana activa tiene cambios sin guardar. ¿Quieres guardarlos antes de cambiar de archivo de proyecto?",
                "Cambios sin guardar",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning);

            if (choice == DialogResult.Cancel)
            {
                return;
            }

            if (choice == DialogResult.Yes)
            {
                SaveWeek(_currentIsoYear, _currentIsoWeek);
            }
        }

        using var dialog = new OpenFileDialog
        {
            Title = "Seleccionar archivo de proyecto",
            Filter = "Archivo de proyecto (*.json)|*.json|Todos los archivos (*.*)|*.*",
            DefaultExt = "json",
            CheckFileExists = false,
            CheckPathExists = true,
            FileName = Path.GetFileName(_workspaceService.CurrentProjectFilePath),
            InitialDirectory = Path.GetDirectoryName(_workspaceService.CurrentProjectFilePath)
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _workspaceService.SwitchProjectFile(dialog.FileName);
        UpdateProjectFileLabel();
        LoadWeek(_currentIsoYear, _currentIsoWeek);
        _statusLabel.Text = $"Proyecto cambiado a {Path.GetFileName(dialog.FileName)}";
    }

    private void UpdateProjectFileLabel()
    {
        var path = _workspaceService.CurrentProjectFilePath;
        _projectFileLabel.Text = $"Archivo: {Path.GetFileName(path)}";
        _toolTip.SetToolTip(_projectFileLabel, path);
    }

}
