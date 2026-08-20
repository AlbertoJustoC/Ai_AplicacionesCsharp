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
    private readonly ComboBox _seriesFieldCombo = new();
    private readonly Button _seriesOptionsButton = new();
    private readonly ContextMenuStrip _seriesOptionsMenu = new();
    private readonly DateTimePicker _fromDatePicker = new();
    private readonly DateTimePicker _toDatePicker = new();
    private readonly CheckBox _useDateRangeCheckBox = new();
    private readonly TrackingLineChartPanel _chartPanel = new();
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
        RebuildSeriesOptions();
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

        _seriesFieldCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _seriesFieldCombo.Width = 180;

        foreach (var field in _optionFields)
        {
            _seriesFieldCombo.Items.Add(field.Label);
        }

        if (_seriesFieldCombo.Items.Count > 0)
        {
            _seriesFieldCombo.SelectedIndex = 0;
        }

        _seriesFieldCombo.SelectedIndexChanged += (_, _) => RebuildSeriesOptions();
        flow.Controls.Add(CreateLabeledControl("Campo de series", _seriesFieldCombo));
        flow.Controls.Add(CreateSeriesOptionsControl());
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

    private static Control CreateLabeledControl(string label, Control control)
    {
        var panel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            Margin = new Padding(0, 0, 16, 8)
        };

        panel.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 2)
        });
        panel.Controls.Add(control);
        return panel;
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

        _chartPanel.Dock = DockStyle.Fill;
        card.Controls.Add(_chartPanel);
        return card;
    }

    private Control CreateSeriesOptionsControl()
    {
        _seriesOptionsMenu.ShowCheckMargin = true;
        _seriesOptionsMenu.ShowImageMargin = false;
        // Keep the dropdown open while the user checks/unchecks several options.
        _seriesOptionsMenu.Closing += (_, e) => e.Cancel = e.CloseReason == ToolStripDropDownCloseReason.ItemClicked;

        _seriesOptionsButton.AutoSize = false;
        _seriesOptionsButton.Width = 240;
        _seriesOptionsButton.Height = 28;
        _seriesOptionsButton.TextAlign = ContentAlignment.MiddleLeft;
        _seriesOptionsButton.FlatStyle = FlatStyle.Flat;
        _seriesOptionsButton.FlatAppearance.BorderSize = 1;
        _seriesOptionsButton.Click += (_, _) => _seriesOptionsMenu.Show(_seriesOptionsButton, new Point(0, _seriesOptionsButton.Height));

        return CreateLabeledControl("Mostrar en el grafico", _seriesOptionsButton);
    }

    private void RebuildSeriesOptions()
    {
        _seriesOptionsMenu.Items.Clear();

        if (_seriesFieldCombo.SelectedIndex < 0)
        {
            _seriesOptionsButton.Text = "Sin campo seleccionado";
            _seriesOptionsButton.Enabled = false;
            RefreshChart();
            return;
        }

        _seriesOptionsButton.Enabled = true;
        var seriesField = _optionFields[_seriesFieldCombo.SelectedIndex];

        var selectAllItem = new ToolStripMenuItem("Seleccionar todas");
        selectAllItem.Click += (_, _) => SetAllSeriesOptions(true);

        var selectNoneItem = new ToolStripMenuItem("Deseleccionar todas");
        selectNoneItem.Click += (_, _) => SetAllSeriesOptions(false);

        _seriesOptionsMenu.Items.Add(selectAllItem);
        _seriesOptionsMenu.Items.Add(selectNoneItem);
        _seriesOptionsMenu.Items.Add(new ToolStripSeparator());

        AddSeriesOptionItem(TrackingChartSeriesBuilder.EmptyValueOption, isChecked: true);

        foreach (var option in FieldOptionsHelper.GetOptions(_project, seriesField))
        {
            AddSeriesOptionItem(option, isChecked: true);
        }

        UpdateSeriesOptionsSummary();
    }

    private void AddSeriesOptionItem(string optionText, bool isChecked)
    {
        var item = new ToolStripMenuItem(optionText) { CheckOnClick = true, Checked = isChecked, Tag = "option" };
        item.CheckedChanged += (_, _) => UpdateSeriesOptionsSummary();
        _seriesOptionsMenu.Items.Add(item);
    }

    private void UpdateSeriesOptionsSummary()
    {
        var optionItems = GetSeriesOptionMenuItems();
        var checkedCount = optionItems.Count(item => item.Checked);
        var seriesField = _optionFields[_seriesFieldCombo.SelectedIndex];
        _seriesOptionsButton.Text = $"{seriesField.Label}: {checkedCount} de {optionItems.Count} seleccionadas";
        RefreshChart();
    }

    private List<ToolStripMenuItem> GetSeriesOptionMenuItems()
    {
        return _seriesOptionsMenu.Items.OfType<ToolStripMenuItem>().Where(item => item.Tag as string == "option").ToList();
    }

    private void SetAllSeriesOptions(bool isChecked)
    {
        foreach (var item in GetSeriesOptionMenuItems())
        {
            item.Checked = isChecked;
        }
    }

    private void RefreshChart()
    {
        if (_dateField is null)
        {
            _chartPanel.SetData("Esta ficha no tiene un campo de fecha configurado.", [], []);
            _summaryLabel.Text = string.Empty;
            return;
        }

        if (_optionFields.Count == 0 || _seriesFieldCombo.SelectedIndex < 0)
        {
            _chartPanel.SetData("No hay campos de lista configurados en el esquema.", [], []);
            _summaryLabel.Text = string.Empty;
            return;
        }

        var seriesField = _optionFields[_seriesFieldCombo.SelectedIndex];
        var selectedOptionValues = GetSeriesOptionMenuItems().Where(item => item.Checked).Select(item => item.Text ?? string.Empty).ToList();

        if (selectedOptionValues.Count == 0)
        {
            _chartPanel.SetData("Selecciona al menos una opcion para mostrar en el grafico.", [], []);
            _summaryLabel.Text = string.Empty;
            return;
        }

        var filteredEntries = _project.Entries.Where(MatchesDateRangeFilter).ToList();
        var (dates, series) = TrackingChartSeriesBuilder.Build(filteredEntries, _dateField, seriesField, selectedOptionValues);
        var legendSeries = BuildLegendSeriesWithMetrics(series);

        var matchingEntryCount = filteredEntries.Count(entry =>
            TryGetEntryDate(entry) is not null
            && selectedOptionValues.Contains(TrackingChartSeriesBuilder.GetDisplayValue(entry, seriesField.Key), StringComparer.OrdinalIgnoreCase));

        _chartPanel.SetData($"{seriesField.Label}: registros acumulados por dia", dates, legendSeries);
        _summaryLabel.Text = $"Mostrando {matchingEntryCount} de {_project.Entries.Count} registros totales";
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

    private void ResetFilters()
    {
        _useDateRangeCheckBox.Checked = false;
        RebuildSeriesOptions();
    }
}
