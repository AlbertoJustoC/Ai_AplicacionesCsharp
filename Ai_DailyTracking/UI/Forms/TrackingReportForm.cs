using Ai_DailyTracking.Application.Services;
using Ai_DailyTracking.Domain.Models;
using Ai_DailyTracking.Infrastructure;
using Ai_DailyTracking.Shared.Helpers;
using Ai_DailyTracking.UI.Controls;

namespace Ai_DailyTracking.UI.Forms;

// Filterable table (all ficha fields) for the entries matching the selected field/options/date range, styled like TrackingChartForm.
public sealed class TrackingReportForm : Form
{
    private readonly TrackingProject _project;
    private readonly TrackingFormSchema _schema;
    private readonly List<TrackingFieldDefinition> _optionFields;
    private readonly TrackingFieldDefinition? _dateField;
    private readonly TrackingFieldDefinition? _statusField;
    private readonly List<TrackingEntry> _matchingEntries = [];
    // 3 independent field+options filter rows combined with AND logic; row 1 keeps the original default (first field selected).
    private readonly List<FieldFilterControls> _fieldFilters = [new(), new(), new()];
    private readonly DateTimePicker _fromDatePicker = new();
    private readonly DateTimePicker _toDatePicker = new();
    private readonly CheckBox _useDateRangeCheckBox = new();
    private readonly DataGridView _resultsGrid = new();
    private readonly Label _summaryLabel = new();

    public TrackingReportForm(TrackingProject project, TrackingFormSchema schema)
    {
        _project = project;
        _schema = schema;
        _optionFields = _schema.Fields.Where(field => field.Type is TrackingFieldType.Option or TrackingFieldType.EditableOption).ToList();
        _dateField = _schema.Fields.FirstOrDefault(field => field.Type == TrackingFieldType.Date);
        _statusField = _schema.Fields.FirstOrDefault(field => string.Equals(field.Key, "status", StringComparison.OrdinalIgnoreCase));

        Text = $"Crear informe - {project.ProjectName}";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1100, 700);
        Size = new Size(1200, 760);
        BackColor = Color.FromArgb(236, 240, 244);
        Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);

        BuildLayout();
        BuildResultsColumns();

        foreach (var filter in _fieldFilters)
        {
            RebuildFilterOptions(filter);
        }
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(18)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        root.Controls.Add(BuildFiltersPanel(), 0, 0);
        root.Controls.Add(BuildResultsPanel(), 0, 1);
        Controls.Add(root);
    }

    private Control BuildFiltersPanel()
    {
        var card = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.White,
            Padding = new Padding(16),
            BorderStyle = BorderStyle.FixedSingle
        };

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };

        for (var i = 0; i < _fieldFilters.Count; i++)
        {
            InitializeFieldFilterCombo(_fieldFilters[i], selectFirstField: i == 0);
        }

        flow.Controls.Add(BuildFieldFiltersGroup());
        flow.Controls.Add(CreateDateRangeControl());

        var resetButton = new Button { Text = "Quitar filtros", AutoSize = true, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 22, 0, 0) };
        resetButton.FlatAppearance.BorderSize = 1;
        resetButton.Click += (_, _) => ResetFilters();
        flow.Controls.Add(resetButton);

        var createPdfButton = new Button { Text = "Crear PDF (A3)", AutoSize = true, FlatStyle = FlatStyle.Flat, Margin = new Padding(8, 22, 0, 0) };
        createPdfButton.FlatAppearance.BorderSize = 1;
        createPdfButton.Click += (_, _) => CreateReportPdf();
        flow.Controls.Add(createPdfButton);

        _summaryLabel.Dock = DockStyle.Top;
        _summaryLabel.AutoSize = true;
        _summaryLabel.Margin = new Padding(0, 10, 0, 0);
        _summaryLabel.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
        _summaryLabel.ForeColor = Color.FromArgb(18, 103, 177);

        card.Controls.Add(_summaryLabel);
        card.Controls.Add(flow);
        return card;
    }

    private void InitializeFieldFilterCombo(FieldFilterControls filter, bool selectFirstField)
    {
        filter.FieldCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        filter.FieldCombo.Width = 180;
        filter.FieldCombo.Items.Add(NoFieldFilterOption);

        foreach (var field in _optionFields)
        {
            filter.FieldCombo.Items.Add(field.Label);
        }

        filter.FieldCombo.SelectedIndex = selectFirstField && _optionFields.Count > 0 ? 1 : 0;
        filter.FieldCombo.SelectedIndexChanged += (_, _) => RebuildFilterOptions(filter);
    }

    // Uses a TableLayoutPanel (instead of stacked rows) so the 3 rows' combo boxes and options buttons
    // form two perfectly aligned, equidistant columns regardless of whether a row shows a label.
    private Control BuildFieldFiltersGroup()
    {
        var table = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = _fieldFilters.Count + 1,
            Margin = new Padding(0, 0, 16, 8)
        };

        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var labelFont = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
        table.Controls.Add(new Label { Text = "Filtrar por campo", AutoSize = true, Font = labelFont, Margin = new Padding(0, 0, 8, 4) }, 0, 0);
        table.Controls.Add(new Label { Text = "Incluir en el informe", AutoSize = true, Font = labelFont, Margin = new Padding(0, 0, 0, 4) }, 1, 0);

        for (var i = 0; i < _fieldFilters.Count; i++)
        {
            var filter = _fieldFilters[i];
            ConfigureFilterOptionsButton(filter);

            filter.FieldCombo.Margin = new Padding(0, 0, 8, 6);
            filter.OptionsButton.Margin = new Padding(0, 0, 0, 6);

            table.Controls.Add(filter.FieldCombo, 0, i + 1);
            table.Controls.Add(filter.OptionsButton, 1, i + 1);
        }

        return table;
    }

    private TrackingFieldDefinition? GetSelectedField(FieldFilterControls filter)
    {
        var index = filter.FieldCombo.SelectedIndex;
        return index <= 0 ? null : _optionFields[index - 1];
    }

    private Control CreateDateRangeControl()
    {
        _useDateRangeCheckBox.Text = "Filtrar por fecha";
        _useDateRangeCheckBox.AutoSize = true;
        _useDateRangeCheckBox.Enabled = _dateField is not null;
        _useDateRangeCheckBox.CheckedChanged += (_, _) =>
        {
            _fromDatePicker.Enabled = _useDateRangeCheckBox.Checked;
            _toDatePicker.Enabled = _useDateRangeCheckBox.Checked;
            RefreshResults();
        };

        _fromDatePicker.Format = DateTimePickerFormat.Custom;
        _fromDatePicker.CustomFormat = "dd MMM yyyy";
        _fromDatePicker.Width = 130;
        _fromDatePicker.Enabled = false;
        _fromDatePicker.Value = DateTime.Now.AddMonths(-1);
        _fromDatePicker.ValueChanged += (_, _) => RefreshResults();

        _toDatePicker.Format = DateTimePickerFormat.Custom;
        _toDatePicker.CustomFormat = "dd MMM yyyy";
        _toDatePicker.Width = 130;
        _toDatePicker.Enabled = false;
        _toDatePicker.ValueChanged += (_, _) => RefreshResults();

        var dateGroup = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            Margin = new Padding(0, 0, 16, 8)
        };

        var checkRow = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        checkRow.Controls.Add(_useDateRangeCheckBox);

        var pickersRow = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        pickersRow.Controls.Add(new Label { Text = "Desde", AutoSize = true, Margin = new Padding(0, 6, 4, 0) });
        pickersRow.Controls.Add(_fromDatePicker);
        pickersRow.Controls.Add(new Label { Text = "Hasta", AutoSize = true, Margin = new Padding(8, 6, 4, 0) });
        pickersRow.Controls.Add(_toDatePicker);

        dateGroup.Controls.Add(checkRow);
        dateGroup.Controls.Add(pickersRow);
        return dateGroup;
    }

    private Control BuildResultsPanel()
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(16),
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 12, 0, 0)
        };

        _resultsGrid.Dock = DockStyle.Fill;
        _resultsGrid.ReadOnly = true;
        _resultsGrid.AllowUserToAddRows = false;
        _resultsGrid.AllowUserToDeleteRows = false;
        _resultsGrid.AllowUserToResizeRows = false;
        _resultsGrid.RowHeadersVisible = false;
        _resultsGrid.BackgroundColor = Color.White;
        _resultsGrid.BorderStyle = BorderStyle.None;
        _resultsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _resultsGrid.AllowUserToResizeColumns = true;
        _resultsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        _resultsGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        _resultsGrid.ShowCellToolTips = true;
        _resultsGrid.CellToolTipTextNeeded += ResultsGrid_CellToolTipTextNeeded;
        _resultsGrid.CellPainting += ResultsGrid_CellPainting;

        card.Controls.Add(_resultsGrid);
        return card;
    }

    // Shows the full cell text as a tooltip only when the column is too narrow to display it.
    private void ResultsGrid_CellToolTipTextNeeded(object? sender, DataGridViewCellToolTipTextNeededEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || _resultsGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value is not string text || string.IsNullOrEmpty(text))
        {
            return;
        }

        var column = _resultsGrid.Columns[e.ColumnIndex];
        var textWidth = TextRenderer.MeasureText(text, _resultsGrid.Font).Width;

        if (textWidth > column.Width - 10)
        {
            e.ToolTipText = text;
        }
    }

    // Paints the status column with a color swatch (TrackingFieldDefinition.OptionColors), matching the ficha combos and history list.
    private void ResultsGrid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (_statusField is null || e.RowIndex < 0 || e.ColumnIndex != _resultsGrid.Columns[_statusField.Key].Index)
        {
            return;
        }

        e.PaintBackground(e.ClipBounds, true);

        var text = e.Value as string ?? string.Empty;
        var swatchColor = OptionColorHelper.GetColor(_statusField, text);
        var swatchRect = new Rectangle(e.CellBounds.Left + 4, e.CellBounds.Top + ((e.CellBounds.Height - 14) / 2), 14, 14);

        using (var swatchBrush = new SolidBrush(swatchColor))
        {
            e.Graphics!.FillRectangle(swatchBrush, swatchRect);
        }

        e.Graphics!.DrawRectangle(Pens.Gray, swatchRect);

        var textRect = new Rectangle(swatchRect.Right + 6, e.CellBounds.Top, e.CellBounds.Right - swatchRect.Right - 6, e.CellBounds.Height);
        TextRenderer.DrawText(e.Graphics!, text, e.CellStyle!.Font ?? _resultsGrid.Font, textRect, e.CellStyle!.ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);

        e.Handled = true;
    }

    private void ConfigureFilterOptionsButton(FieldFilterControls filter)
    {
        filter.OptionsMenu.ShowCheckMargin = true;
        filter.OptionsMenu.ShowImageMargin = false;
        // Keep the dropdown open while the user checks/unchecks several options.
        filter.OptionsMenu.Closing += (_, e) => e.Cancel = e.CloseReason == ToolStripDropDownCloseReason.ItemClicked;

        filter.OptionsButton.AutoSize = false;
        filter.OptionsButton.Width = 240;
        filter.OptionsButton.Height = 28;
        filter.OptionsButton.TextAlign = ContentAlignment.MiddleLeft;
        filter.OptionsButton.FlatStyle = FlatStyle.Flat;
        filter.OptionsButton.FlatAppearance.BorderSize = 1;
        filter.OptionsButton.Click += (_, _) => filter.OptionsMenu.Show(filter.OptionsButton, new Point(0, filter.OptionsButton.Height));
    }

    private void RebuildFilterOptions(FieldFilterControls filter)
    {
        filter.OptionsMenu.Items.Clear();
        var field = GetSelectedField(filter);

        if (field is null)
        {
            filter.OptionsButton.Text = "Sin filtro";
            filter.OptionsButton.Enabled = false;
            RefreshResults();
            return;
        }

        filter.OptionsButton.Enabled = true;

        var selectAllItem = new ToolStripMenuItem("Seleccionar todas");
        selectAllItem.Click += (_, _) => SetAllFilterOptions(filter, true);

        var selectNoneItem = new ToolStripMenuItem("Deseleccionar todas");
        selectNoneItem.Click += (_, _) => SetAllFilterOptions(filter, false);

        filter.OptionsMenu.Items.Add(selectAllItem);
        filter.OptionsMenu.Items.Add(selectNoneItem);
        filter.OptionsMenu.Items.Add(new ToolStripSeparator());

        AddFilterOptionItem(filter, TrackingChartSeriesBuilder.EmptyValueOption, isChecked: true);

        foreach (var option in FieldOptionsHelper.GetOptions(_project, field))
        {
            AddFilterOptionItem(filter, option, isChecked: true);
        }

        UpdateFilterOptionsSummary(filter);
    }

    private void AddFilterOptionItem(FieldFilterControls filter, string optionText, bool isChecked)
    {
        var item = new ToolStripMenuItem(optionText) { CheckOnClick = true, Checked = isChecked, Tag = "option" };
        item.CheckedChanged += (_, _) => UpdateFilterOptionsSummary(filter);
        filter.OptionsMenu.Items.Add(item);
    }

    private void UpdateFilterOptionsSummary(FieldFilterControls filter)
    {
        var field = GetSelectedField(filter);

        if (field is null)
        {
            RefreshResults();
            return;
        }

        var optionItems = GetOptionMenuItems(filter);
        var checkedCount = optionItems.Count(item => item.Checked);
        filter.OptionsButton.Text = $"{field.Label}: {checkedCount} de {optionItems.Count} seleccionadas";
        RefreshResults();
    }

    private static List<ToolStripMenuItem> GetOptionMenuItems(FieldFilterControls filter)
    {
        return filter.OptionsMenu.Items.OfType<ToolStripMenuItem>().Where(item => item.Tag as string == "option").ToList();
    }

    private static void SetAllFilterOptions(FieldFilterControls filter, bool isChecked)
    {
        foreach (var item in GetOptionMenuItems(filter))
        {
            item.Checked = isChecked;
        }
    }

    private void BuildResultsColumns()
    {
        _resultsGrid.Columns.Clear();
        _resultsGrid.Columns.Add("EntryNumber", "ID");
        _resultsGrid.Columns["EntryNumber"].Width = 60;

        foreach (var field in _schema.Fields)
        {
            _resultsGrid.Columns.Add(field.Key, field.Label);
            _resultsGrid.Columns[field.Key].Width = 140;
        }
    }

    private void RefreshResults()
    {
        _matchingEntries.Clear();

        if (_optionFields.Count == 0)
        {
            _resultsGrid.Rows.Clear();
            _summaryLabel.Text = "No hay campos de lista configurados en el esquema.";
            return;
        }

        var activeFilters = _fieldFilters
            .Select(filter => (Filter: filter, Field: GetSelectedField(filter)))
            .Where(pair => pair.Field is not null)
            .ToList();

        foreach (var (filter, field) in activeFilters)
        {
            if (GetOptionMenuItems(filter).All(item => !item.Checked))
            {
                _resultsGrid.Rows.Clear();
                _summaryLabel.Text = $"Selecciona al menos una opcion para \"{field!.Label}\".";
                return;
            }
        }

        var matchingEntries = _project.Entries
            .Where(MatchesDateRangeFilter)
            .Where(entry => activeFilters.All(pair => MatchesFieldFilter(entry, pair.Filter, pair.Field!)))
            .OrderByDescending(entry => entry.UpdatedAtLocal)
            .ToList();

        _matchingEntries.AddRange(matchingEntries);
        PopulateGrid(matchingEntries);
        _summaryLabel.Text = $"Mostrando {matchingEntries.Count} de {_project.Entries.Count} registros totales";
    }

    private static bool MatchesFieldFilter(TrackingEntry entry, FieldFilterControls filter, TrackingFieldDefinition field)
    {
        var selectedOptionValues = GetOptionMenuItems(filter).Where(item => item.Checked).Select(item => item.Text ?? string.Empty).ToList();
        return selectedOptionValues.Contains(TrackingChartSeriesBuilder.GetDisplayValue(entry, field.Key), StringComparer.OrdinalIgnoreCase);
    }

    // Renders the chart using the row 1 filter field (same as the original single-filter behavior), plus the same date-range/AND filters as the results grid.
    private void CreateReportPdf()
    {
        var primaryFilter = _fieldFilters[0];
        var primaryField = GetSelectedField(primaryFilter);

        if (primaryField is null || _matchingEntries.Count == 0)
        {
            MessageBox.Show(this, "No hay registros que cumplan los filtros actuales para generar el PDF.", "Crear informe", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var selectedOptionValues = GetOptionMenuItems(primaryFilter).Where(item => item.Checked).Select(item => item.Text ?? string.Empty).ToList();
        var (chartDates, chartSeries) = TrackingChartSeriesBuilder.Build(_matchingEntries, _dateField, primaryField, selectedOptionValues);

        using var chartBitmap = RenderChartBitmap($"{primaryField.Label}: registros acumulados por dia", chartDates, chartSeries);

        var exporter = new TrackingReportPdfExporter();
        exporter.TryExport(this, _project.ProjectName, _schema, _matchingEntries, chartBitmap);
    }

    private static Bitmap? RenderChartBitmap(string title, IReadOnlyList<DateTime> dates, IReadOnlyList<(string Label, IReadOnlyList<int> Counts)> series)
    {
        if (dates.Count == 0 || series.Count == 0)
        {
            return null;
        }

        using var chartPanel = new TrackingLineChartPanel { Size = new Size(2400, 1350) };
        chartPanel.SetData(title, dates, series);
        chartPanel.CreateControl();

        var bitmap = new Bitmap(chartPanel.Width, chartPanel.Height);
        chartPanel.DrawToBitmap(bitmap, new Rectangle(Point.Empty, chartPanel.Size));
        return bitmap;
    }

    private void PopulateGrid(List<TrackingEntry> entries)
    {
        _resultsGrid.Rows.Clear();

        foreach (var entry in entries)
        {
            var rowIndex = _resultsGrid.Rows.Add();
            var row = _resultsGrid.Rows[rowIndex];
            row.Cells["EntryNumber"].Value = entry.EntryNumber;

            foreach (var field in _schema.Fields)
            {
                row.Cells[field.Key].Value = GetFormattedFieldValue(entry, field);
            }
        }

        _resultsGrid.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.DisplayedCells);
    }

    private static string GetFormattedFieldValue(TrackingEntry entry, TrackingFieldDefinition field)
    {
        if (!entry.Values.TryGetValue(field.Key, out var rawValue) || string.IsNullOrWhiteSpace(rawValue))
        {
            return string.Empty;
        }

        if (field.Type == TrackingFieldType.Date && DateTime.TryParse(rawValue, out var parsedDate))
        {
            return parsedDate.ToString("dd/MM/yyyy");
        }

        return rawValue;
    }

    private bool MatchesDateRangeFilter(TrackingEntry entry)
    {
        if (!_useDateRangeCheckBox.Checked)
        {
            return true;
        }

        var entryDate = TryGetEntryDate(entry);
        return entryDate is not null && entryDate.Value >= _fromDatePicker.Value.Date && entryDate.Value <= _toDatePicker.Value.Date;
    }

    private DateTime? TryGetEntryDate(TrackingEntry entry)
    {
        if (_dateField is null || !entry.Values.TryGetValue(_dateField.Key, out var rawDate) || !DateTime.TryParse(rawDate, out var parsedDate))
        {
            return null;
        }

        return parsedDate.Date;
    }

    private void ResetFilters()
    {
        _useDateRangeCheckBox.Checked = false;

        foreach (var filter in _fieldFilters)
        {
            RebuildFilterOptions(filter);
        }
    }

    private const string NoFieldFilterOption = "(Sin filtro)";

    // Holds the field selector + checkbox-options dropdown for one AND filter row.
    private sealed class FieldFilterControls
    {
        public ComboBox FieldCombo { get; } = new();

        public Button OptionsButton { get; } = new();

        public ContextMenuStrip OptionsMenu { get; } = new();
    }
}
