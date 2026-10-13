using Ai_DailyTracking.Application.Services;
using Ai_DailyTracking.Domain.Models;
using Ai_DailyTracking.Shared.Helpers;
using Ai_DailyTracking.UI.Controls;

namespace Ai_DailyTracking.UI.Forms;

public sealed class TrackingChartForm : Form
{
    private readonly TrackingProject _project;
    private readonly TrackingFormSchema _schema;
    private readonly List<TrackingFieldDefinition> _optionFields;
    private readonly TrackingFieldDefinition? _dateField;
    private readonly List<FieldFilterControls> _fieldFilters = [new(), new(), new()];
    private readonly DateTimePicker _fromDatePicker = new();
    private readonly DateTimePicker _toDatePicker = new();
    private readonly CheckBox _useDateRangeCheckBox = new();
    private readonly TrackingLineChartPanel _chartPanel = new();
    private readonly TrackingTimelinePanel _timelinePanel = new();
    private readonly Button _toggleChartButton = new();
    private bool _showTimeline;
    private readonly Label _summaryLabel = new();

    public TrackingChartForm(TrackingProject project, TrackingFormSchema schema)
    {
        _project = project;
        _schema = schema;
        _optionFields = _schema.Fields.Where(field => field.Type is TrackingFieldType.Option or TrackingFieldType.EditableOption).ToList();
        _dateField = _schema.Fields.FirstOrDefault(field => field.Type == TrackingFieldType.Date);

        Text = $"Panel grafico - {project.ProjectName}";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1100, 660);
        BackColor = Color.FromArgb(236, 240, 244);
        Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);

        BuildLayout();
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
        root.Controls.Add(BuildChartPanel(), 0, 1);
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

        _summaryLabel.Dock = DockStyle.Top;
        _summaryLabel.AutoSize = true;
        _summaryLabel.Margin = new Padding(0, 10, 0, 0);
        _summaryLabel.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
        _summaryLabel.ForeColor = Color.FromArgb(18, 103, 177);

        card.Controls.Add(_summaryLabel);
        card.Controls.Add(flow);
        return card;
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
            RefreshChart();
        };

        _fromDatePicker.Format = DateTimePickerFormat.Custom;
        _fromDatePicker.CustomFormat = "dd MMM yyyy";
        _fromDatePicker.Width = 130;
        _fromDatePicker.Enabled = false;
        _fromDatePicker.Value = DateTime.Now.AddMonths(-1);
        _fromDatePicker.ValueChanged += (_, _) => RefreshChart();

        _toDatePicker.Format = DateTimePickerFormat.Custom;
        _toDatePicker.CustomFormat = "dd MMM yyyy";
        _toDatePicker.Width = 130;
        _toDatePicker.Enabled = false;
        _toDatePicker.ValueChanged += (_, _) => RefreshChart();

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

    private void InitializeFieldFilterCombo(FieldFilterControls filter, bool selectFirstField)
    {
        filter.FieldCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        filter.FieldCombo.Width = 180;
        filter.FieldCombo.Items.Add(NoFieldFilterOption);

        foreach (var field in _optionFields)
        {
            filter.FieldCombo.Items.Add(field.Label);
        }

        filter.FieldCombo.SelectedIndex = selectFirstField && _optionFields.Count > 0
            ? Math.Max(1, _optionFields.FindIndex(field => string.Equals(field.Key, "status", StringComparison.OrdinalIgnoreCase)) + 1)
            : 0;
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

    private Control BuildChartPanel()
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(16),
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 12, 0, 0)
        };

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _toggleChartButton.Text = "Ver líneas de tiempo";
        _toggleChartButton.AutoSize = true;
        _toggleChartButton.FlatStyle = FlatStyle.Flat;
        _toggleChartButton.Margin = new Padding(0, 0, 0, 8);
        _toggleChartButton.Click += (_, _) =>
        {
            _showTimeline = !_showTimeline;
            _chartPanel.Visible = !_showTimeline;
            _timelinePanel.Visible = _showTimeline;
            _toggleChartButton.Text = _showTimeline ? "Ver gráfico acumulado" : "Ver líneas de tiempo";
        };

        var charts = new Panel { Dock = DockStyle.Fill };
        _chartPanel.Dock = DockStyle.Fill;
        _timelinePanel.Dock = DockStyle.Fill;
        _timelinePanel.Visible = false;
        charts.Controls.Add(_chartPanel);
        charts.Controls.Add(_timelinePanel);
        layout.Controls.Add(_toggleChartButton, 0, 0);
        layout.Controls.Add(charts, 0, 1);
        card.Controls.Add(layout);
        return card;
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
            RefreshChart();
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
            RefreshChart();
            return;
        }

        var optionItems = GetOptionMenuItems(filter);
        var checkedCount = optionItems.Count(item => item.Checked);
        filter.OptionsButton.Text = $"{field.Label}: {checkedCount} de {optionItems.Count} seleccionadas";
        RefreshChart();
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

    private void RefreshChart()
    {
        if (_dateField is null)
        {
            _chartPanel.SetData("Esta ficha no tiene un campo de fecha configurado.", [], []);
            _timelinePanel.SetData([], null, "Esta ficha no tiene un campo de fecha configurado.");
            _summaryLabel.Text = string.Empty;
            return;
        }

        if (_optionFields.Count == 0)
        {
            _chartPanel.SetData("No hay campos de lista configurados en el esquema.", [], []);
            _timelinePanel.SetData([], null, "No hay campos de lista configurados en el esquema.");
            _summaryLabel.Text = string.Empty;
            return;
        }

        var primaryFilter = _fieldFilters[0];
        var seriesField = GetSelectedField(primaryFilter);
        if (seriesField is null)
        {
            _chartPanel.SetData("Selecciona un campo para mostrar en el grafico.", [], []);
            _timelinePanel.SetData([], null, "Selecciona un campo para mostrar en el grafico.");
            _summaryLabel.Text = string.Empty;
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
                _chartPanel.SetData($"Selecciona al menos una opcion para \"{field!.Label}\".", [], []);
                _timelinePanel.SetData([], null, $"Selecciona al menos una opción para \"{field.Label}\".");
                _summaryLabel.Text = string.Empty;
                return;
            }
        }

        var selectedOptionValues = GetOptionMenuItems(primaryFilter).Where(item => item.Checked).Select(item => item.Text ?? string.Empty).ToList();
        var filteredEntries = _project.Entries
            .Where(MatchesDateRangeFilter)
            .Where(entry => activeFilters.All(pair => MatchesFieldFilter(entry, pair.Filter, pair.Field!)))
            .ToList();
        var (dates, series) = TrackingChartSeriesBuilder.Build(filteredEntries, _dateField, seriesField, selectedOptionValues);
        var legendSeries = BuildLegendSeriesWithMetrics(series);

        _chartPanel.SetData($"{seriesField.Label}: registros acumulados por dia", dates, legendSeries);
        var statusField = _schema.Fields.FirstOrDefault(field => string.Equals(field.Key, "status", StringComparison.OrdinalIgnoreCase));
        _timelinePanel.SetData(TrackingTimelineBuilder.Build(filteredEntries, _schema, _dateField.Key), statusField);
        _summaryLabel.Text = $"Mostrando {filteredEntries.Count} de {_project.Entries.Count} registros totales";
    }

    private static IReadOnlyList<(string Label, IReadOnlyList<int> Counts)> BuildLegendSeriesWithMetrics(
        IReadOnlyList<(string Label, IReadOnlyList<int> Counts)> series)
    {
        if (series.Count == 0)
        {
            return series;
        }

        var totalSeries = series.Last();
        var totalCount = totalSeries.Counts.Count > 0 ? totalSeries.Counts[^1] : 0;
        var result = new List<(string Label, IReadOnlyList<int> Counts)>(series.Count);

        foreach (var item in series)
        {
            var finalCount = item.Counts.Count > 0 ? item.Counts[^1] : 0;

            if (string.Equals(item.Label, "Total", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(($"Total ({finalCount})", item.Counts));
                continue;
            }

            var percentage = totalCount == 0 ? 0D : finalCount * 100D / totalCount;
            result.Add(($"{item.Label} ({finalCount} - {percentage:0.#}%)", item.Counts));
        }

        return result;
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

    private static bool MatchesFieldFilter(TrackingEntry entry, FieldFilterControls filter, TrackingFieldDefinition field)
    {
        var selectedOptionValues = GetOptionMenuItems(filter).Where(item => item.Checked).Select(item => item.Text ?? string.Empty).ToList();
        return selectedOptionValues.Contains(TrackingChartSeriesBuilder.GetDisplayValue(entry, field.Key), StringComparer.OrdinalIgnoreCase);
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

    private sealed class FieldFilterControls
    {
        public ComboBox FieldCombo { get; } = new();
        public Button OptionsButton { get; } = new();
        public ContextMenuStrip OptionsMenu { get; } = new();
    }
}
