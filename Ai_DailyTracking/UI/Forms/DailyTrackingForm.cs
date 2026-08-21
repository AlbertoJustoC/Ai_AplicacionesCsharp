using Ai_DailyTracking.Application.Services;
using Ai_DailyTracking.Domain.Models;
using Ai_DailyTracking.Shared.Helpers;
using System.Diagnostics;

namespace Ai_DailyTracking.UI.Forms;

public sealed class DailyTrackingForm : Form
{
    private const string InputReceivedPathKey = "inputReceivedPath";
    private const string NotesFieldKey = "notes";
    private const string StatusFieldKey = "status";
    private const string UpdatedDateFieldKey = "updatedDate";
    private const string UpdatedDateAutoKey = "updatedDateAuto";

    private readonly ProjectWorkspaceService _workspaceService;
    private readonly TrackingFormSchema _schema;
    private readonly Dictionary<string, Control> _fieldControls = new(StringComparer.OrdinalIgnoreCase);
    private readonly ComboBox _projectSelector = new();
    private readonly Button _newProjectButton = new();
    private readonly Button _deleteProjectButton = new();
    private readonly Button _manageProjectUsersButton = new();
    private readonly Button _newEntryButton = new();
    private readonly Button _deleteEntryButton = new();
    private readonly Button _viewChartButton = new();
    private readonly Button _createReportButton = new();
    private readonly Button _chooseProjectsFolderButton = new();
    private readonly Panel _folderButtonPanel = new();
    private readonly Label _projectSummaryLabel = new();
    private readonly Label _autosaveLabel = new();
    private readonly Label _projectsFolderPathLabel = new();
    private readonly Label _inputReceivedPathLabel = new();
    private readonly Button _inputReceivedButton = new();
    private readonly CheckBox _updatedDateAutoCheckBox = new();
    private readonly ListView _entriesListView = new();
    private readonly ComboBox _statusFilterCombo = new();
    private readonly TableLayoutPanel _editorLayout = new();
    private readonly ToolTip _toolTip = new();
    private readonly TrackingFieldDefinition? _statusFieldDefinition;

    private bool _suppressEvents;
    private bool _suppressUpdatedDateAutoChanged;
    private bool _hasUnsavedChanges;
    private bool _forcingInputReceivedSelection;
    private bool _activityEditedPendingInputPathPrompt;
    private TrackingProject? _currentProject;
    private TrackingEntry? _currentEntry;
    private string? _statusFilterValue;
    private int _sortColumnIndex = -1;
    private SortOrder _sortOrder = SortOrder.None;

    public DailyTrackingForm(ProjectWorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
        _schema = _workspaceService.LoadSchema();
        _statusFieldDefinition = _schema.Fields.FirstOrDefault(field => string.Equals(field.Key, "status", StringComparison.OrdinalIgnoreCase));

        Text = "Seguimiento diario de proyectos";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1360, 700);
        Size = new Size(1360, 900);
        BackColor = Color.FromArgb(236, 240, 244);
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        ApplyExecutableIcon();

        BuildLayout();
        BuildDynamicEditor();

        Load += DailyTrackingForm_Load;
        FormClosing += DailyTrackingForm_FormClosing;
    }

    private void BuildLayout()
    {
        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = BackColor
        };
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 130F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        rootLayout.Controls.Add(CreateHeroPanel(), 0, 0);
        rootLayout.Controls.Add(CreateToolbarPanel(), 0, 1);
        rootLayout.Controls.Add(CreateContentPanel(), 0, 2);

        Controls.Add(rootLayout);
    }

    private Control CreateHeroPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(22, 58, 92),
            Padding = new Padding(24, 18, 24, 18)
        };

        ConfigureActionButton(_chooseProjectsFolderButton, "Carpeta de proyectos", Color.White, Color.FromArgb(22, 58, 92), larger: true);
        _chooseProjectsFolderButton.Click += ChooseProjectsFolderButton_Click;
        _chooseProjectsFolderButton.Margin = new Padding(0);
        _chooseProjectsFolderButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        // Keep the folder controls inside the shorter header.
        _chooseProjectsFolderButton.Top = 5;

        _folderButtonPanel.Dock = DockStyle.Right;

        _projectsFolderPathLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _projectsFolderPathLabel.Top = _chooseProjectsFolderButton.Top + _chooseProjectsFolderButton.Height + 2;
        _projectsFolderPathLabel.Height = 36;
        _projectsFolderPathLabel.TextAlign = ContentAlignment.MiddleRight;
        _projectsFolderPathLabel.AutoEllipsis = true;
        _projectsFolderPathLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point);
        _projectsFolderPathLabel.ForeColor = Color.FromArgb(190, 202, 214);
        _projectsFolderPathLabel.Cursor = Cursors.Hand;
        _projectsFolderPathLabel.Click += ProjectsFolderPathLabel_Click;

        _folderButtonPanel.Controls.Add(_chooseProjectsFolderButton);
        _folderButtonPanel.Controls.Add(_projectsFolderPathLabel);

        UpdateProjectsFolderDisplay();

        var titleLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 38,
            Text = "Seguimiento Diario de Tareas de Proyecto",
            Font = new Font("Segoe UI Semibold", 22F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = Color.White
        };

        var projectSelectorRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
              Margin = new Padding(0, 12, 0, 0),
              Padding = new Padding(0, 5, 0, 0)
        };

        _projectSelector.Width = 260;
        _projectSelector.Height = 30;
        _projectSelector.Margin = new Padding(0, 12, 16, 0);
        _projectSelector.DropDownStyle = ComboBoxStyle.DropDownList;
        _projectSelector.SelectedIndexChanged += ProjectSelector_SelectedIndexChanged;

        ConfigureActionButton(_manageProjectUsersButton, "Usuarios del proyecto", Color.FromArgb(96, 100, 112), larger: true);
        _manageProjectUsersButton.Margin = new Padding(0, 10, 8, 0);
        _manageProjectUsersButton.Click += ManageProjectUsersButton_Click;

        ConfigureActionButton(_newProjectButton, "Nuevo proyecto", Color.FromArgb(18, 103, 177), larger: true);
        _newProjectButton.Margin = new Padding(0, 10, 8, 0);
        _newProjectButton.Click += NewProjectButton_Click;

        ConfigureActionButton(_deleteProjectButton, "Eliminar proyecto", Color.FromArgb(178, 34, 52), larger: true);
        _deleteProjectButton.TextAlign = ContentAlignment.MiddleCenter;
        _deleteProjectButton.AutoSize = false;
        _deleteProjectButton.Width = 145;
        _deleteProjectButton.Margin = new Padding(0, 10, 8, 0);
        _deleteProjectButton.Click += DeleteProjectButton_Click;

        projectSelectorRow.Controls.Add(_projectSelector);
        projectSelectorRow.Controls.Add(_manageProjectUsersButton);
        projectSelectorRow.Controls.Add(_newProjectButton);
        projectSelectorRow.Controls.Add(_deleteProjectButton);

        var textPanel = new Panel { Dock = DockStyle.Fill };
        textPanel.Controls.Add(projectSelectorRow);
        textPanel.Controls.Add(titleLabel);

        panel.Controls.Add(textPanel);
        panel.Controls.Add(_folderButtonPanel);
        return panel;
    }

    private Control CreateToolbarPanel()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(18, 10, 18, 10),
            BackColor = Color.FromArgb(236, 240, 244)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        var buttonsFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        ConfigureActionButton(_newEntryButton, "Nuevo registro", Color.FromArgb(232, 144, 34));
        _newEntryButton.Click += NewEntryButton_Click;

        ConfigureActionButton(_deleteEntryButton, "Eliminar registro", Color.FromArgb(178, 34, 52));
        _deleteEntryButton.Click += DeleteEntryButton_Click;

        ConfigureActionButton(_viewChartButton, "Ver grafico", Color.FromArgb(46, 139, 87));
        _viewChartButton.Click += ViewChartButton_Click;

        ConfigureActionButton(_createReportButton, "Crear informe", Color.FromArgb(96, 74, 158));
        _createReportButton.Click += CreateReportButton_Click;

        buttonsFlow.Controls.Add(_newEntryButton);
        buttonsFlow.Controls.Add(_deleteEntryButton);
        buttonsFlow.Controls.Add(_viewChartButton);
        buttonsFlow.Controls.Add(_createReportButton);

        var infoPanel = new Panel { Dock = DockStyle.Fill };
        _projectSummaryLabel.Dock = DockStyle.Top;
        _projectSummaryLabel.Height = 26;
        _projectSummaryLabel.Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold, GraphicsUnit.Point);

        _autosaveLabel.Dock = DockStyle.Top;
        _autosaveLabel.Height = 24;
        _autosaveLabel.ForeColor = Color.FromArgb(78, 86, 99);

        infoPanel.Controls.Add(_autosaveLabel);
        infoPanel.Controls.Add(_projectSummaryLabel);

        layout.Controls.Add(buttonsFlow, 0, 0);
        layout.Controls.Add(infoPanel, 1, 0);
        return layout;
    }

    private void ApplyExecutableIcon()
    {
        try
        {
            Icon = Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath);
        }
        catch
        {
            // Keep default icon if extraction fails.
        }
    }

    private static void ConfigureActionButton(Button button, string text, Color backColor, Color? foreColor = null, bool larger = false)
    {
        button.Text = text;
        button.AutoSize = true;
        button.Height = 34;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = backColor;
        button.ForeColor = foreColor ?? Color.White;
        button.Margin = new Padding(0, 0, 8, 0);
        button.Padding = larger ? new Padding(18, 4, 18, 4) : new Padding(10, 4, 10, 4);
    }

    private Control CreateContentPanel()
    {
        // Dock + Splitter (not SplitContainer) avoids SplitContainer's fragile min-size/splitter validation at construction time.
        var container = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BackColor,
            Padding = new Padding(18, 0, 18, 18)
        };

        var editorPanel = CreateEditorPanel();
        editorPanel.Dock = DockStyle.Fill;

        var splitter = new Splitter
        {
            Dock = DockStyle.Left,
            Width = 8,
            BackColor = Color.FromArgb(210, 216, 222),
            MinSize = 280,
            MinExtra = 480
        };

        var entriesPanel = CreateEntriesPanel();
        entriesPanel.Dock = DockStyle.Left;
        entriesPanel.Width = 480;

        // Add order matters for Dock layout: Fill first, then the Left-docked controls, outermost added last.
        container.Controls.Add(editorPanel);
        container.Controls.Add(splitter);
        container.Controls.Add(entriesPanel);
        return container;
    }

    private Control CreateEntriesPanel()
    {
        var card = CreateCardPanel();
        card.Padding = new Padding(16);

        var titleLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 32,
            Text = "Historico del proyecto",
            Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold, GraphicsUnit.Point)
        };

        var filterRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 8)
        };

        var filterLabel = new Label
        {
            Text = "Filtrar por estado:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Padding = new Padding(0, 7, 6, 0)
        };

        _statusFilterCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _statusFilterCombo.Width = 170;
        _statusFilterCombo.Items.Add("(Todos)");

        if (_statusFieldDefinition is not null)
        {
            foreach (var option in _statusFieldDefinition.Options)
            {
                _statusFilterCombo.Items.Add(option);
            }

            if (_statusFieldDefinition.OptionColors is { Count: > 0 })
            {
                _statusFilterCombo.DrawMode = DrawMode.OwnerDrawFixed;
                _statusFilterCombo.DrawItem += (s, e) => DrawColorCodedComboItem(_statusFieldDefinition, _statusFilterCombo, e);
            }
        }

        _statusFilterCombo.SelectedIndex = 0;
        _statusFilterCombo.SelectedIndexChanged += StatusFilterCombo_SelectedIndexChanged;

        filterRow.Controls.Add(filterLabel);
        filterRow.Controls.Add(_statusFilterCombo);

        var headerPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        headerPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        headerPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        headerPanel.Controls.Add(titleLabel, 0, 0);
        headerPanel.Controls.Add(filterRow, 0, 1);

        _entriesListView.Dock = DockStyle.Fill;
        _entriesListView.View = View.Details;
        _entriesListView.FullRowSelect = true;
        _entriesListView.HideSelection = false;
        _entriesListView.MultiSelect = false;
        _entriesListView.BorderStyle = BorderStyle.None;
        _entriesListView.OwnerDraw = true;
        _entriesListView.Columns.Add("ID", 50);
        _entriesListView.Columns.Add("Fecha", 95);
        _entriesListView.Columns.Add("Actividad", 170);
        _entriesListView.Columns.Add("Estado", 100);
        _entriesListView.SelectedIndexChanged += EntriesListView_SelectedIndexChanged;
        _entriesListView.Resize += EntriesListView_Resize;
        _entriesListView.ColumnClick += EntriesListView_ColumnClick;
        _entriesListView.DrawColumnHeader += (s, e) => e.DrawDefault = true;
        _entriesListView.DrawItem += (s, e) => e.DrawDefault = true;
        _entriesListView.DrawSubItem += EntriesListView_DrawSubItem;

        card.Controls.Add(_entriesListView);
        card.Controls.Add(headerPanel);
        return card;
    }

    private Control CreateEditorPanel()
    {
        var card = CreateCardPanel();
        card.Padding = new Padding(16, 12, 16, 12);

        var headingLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Text = "Ficha de seguimiento",
            Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold, GraphicsUnit.Point)
        };

        _editorLayout.Dock = DockStyle.Top;
        _editorLayout.AutoSize = true;
        _editorLayout.AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var scrollPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(0, 8, 0, 0)
        };
        scrollPanel.Controls.Add(_editorLayout);

        card.Controls.Add(scrollPanel);
        card.Controls.Add(headingLabel);
        return card;
    }

    private void BuildDynamicEditor()
    {
        _editorLayout.ColumnCount = 1;
        _editorLayout.ColumnStyles.Clear();
        _editorLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _editorLayout.RowCount = 0;
        _editorLayout.RowStyles.Clear();

        // Consecutive compact fields (text/option/date) share one row unless a field opts out via StartsNewRow;
        // each long-text field always gets its own full-width row.
        var compactGroup = new List<TrackingFieldDefinition>();

        foreach (var field in _schema.Fields)
        {
            if (field.Type == TrackingFieldType.LongText)
            {
                FlushCompactFieldGroup(compactGroup);
                AddEditorRow(CreateFieldCard(field));

                if (IsActivityField(field))
                {
                    AddEditorRow(CreateInputReceivedRow());
                }

                continue;
            }

            if (field.StartsNewRow)
            {
                FlushCompactFieldGroup(compactGroup);
            }

            compactGroup.Add(field);
        }

        FlushCompactFieldGroup(compactGroup);
    }

    private void FlushCompactFieldGroup(List<TrackingFieldDefinition> compactGroup)
    {
        if (compactGroup.Count == 0)
        {
            return;
        }

        AddEditorRow(CreateFieldRowPanel(compactGroup));
        compactGroup.Clear();
    }

    private void AddEditorRow(Control rowContent)
    {
        var rowIndex = _editorLayout.RowCount;
        _editorLayout.RowCount = rowIndex + 1;
        _editorLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rowContent.Dock = DockStyle.Fill;
        _editorLayout.Controls.Add(rowContent, 0, rowIndex);
    }

    private Control CreateFieldRowPanel(IReadOnlyList<TrackingFieldDefinition> fields)
    {
        var rowPanel = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = fields.Count,
            RowCount = 1,
            Margin = new Padding(0)
        };
        rowPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        for (var i = 0; i < fields.Count; i++)
        {
            rowPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / fields.Count));

            var card = CreateFieldCard(fields[i]);
            card.Dock = DockStyle.Fill;
            rowPanel.Controls.Add(card, i, 0);
        }

        return rowPanel;
    }

    private Control CreateFieldCard(TrackingFieldDefinition field)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            Height = GetFieldCardHeight(field),
            BackColor = Color.FromArgb(249, 251, 252),
            Margin = new Padding(4),
            Padding = new Padding(10)
        };

        var label = new Label
        {
            Dock = DockStyle.Top,
            Height = 20,
            Text = field.Required ? $"{field.Label} *" : field.Label,
            Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold, GraphicsUnit.Point)
        };

        var editor = CreateEditorControl(field);
        editor.Dock = DockStyle.Bottom;
        editor.Tag = field.Key;

        if (IsUpdatedDateField(field))
        {
            card.Controls.Add(CreateUpdatedDateAutoToggle());
        }

        card.Controls.Add(editor);
        card.Controls.Add(label);
        _fieldControls[field.Key] = editor;
        return card;
    }

    private static int GetFieldCardHeight(TrackingFieldDefinition field)
    {
        if (IsUpdatedDateField(field))
        {
            return 100;
        }

        if (field.Type != TrackingFieldType.LongText)
        {
            return 82;
        }

        if (IsCommentsField(field))
        {
            return 108;
        }

        return 86;
    }

    private Control CreateEditorControl(TrackingFieldDefinition field)
    {
        switch (field.Type)
        {
            case TrackingFieldType.Date:
                var datePicker = new DateTimePicker
                {
                    Height = 34,
                    Format = DateTimePickerFormat.Custom,
                    CustomFormat = "dd MMM yyyy",
                    ShowCheckBox = true,
                    Checked = false
                };

                if (IsUpdatedDateField(field))
                {
                    datePicker.ValueChanged += (s, e) => UpdatedDatePicker_ValueChanged(datePicker);
                }

                datePicker.ValueChanged += (s, e) => EnforceDependentMinimumDates(field, datePicker);
                datePicker.ValueChanged += FieldValueChanged;
                return datePicker;

            case TrackingFieldType.Option:
                var comboBox = new ComboBox
                {
                    Height = 34,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                comboBox.Items.Add(string.Empty);

                foreach (var option in field.Options)
                {
                    comboBox.Items.Add(option);
                }

                if (field.OptionColors is { Count: > 0 })
                {
                    comboBox.DrawMode = DrawMode.OwnerDrawFixed;
                    comboBox.DrawItem += (s, e) => DrawColorCodedComboItem(field, comboBox, e);
                }

                comboBox.SelectedIndexChanged += FieldValueChanged;
                return comboBox;

            case TrackingFieldType.EditableOption:
                var editableCombo = new ComboBox
                {
                    Height = 34,
                    DropDownStyle = ComboBoxStyle.DropDown,
                    AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                    AutoCompleteSource = AutoCompleteSource.ListItems
                };

                foreach (var option in field.Options)
                {
                    editableCombo.Items.Add(option);
                }

                editableCombo.TextChanged += FieldValueChanged;
                editableCombo.Leave += (s, e) => CommitEditableOptionValue(field, editableCombo);

                var editableOptionsMenu = new ContextMenuStrip();
                editableOptionsMenu.Opening += (s, e) => BuildEditableOptionContextMenu(field, editableCombo, editableOptionsMenu);
                editableCombo.ContextMenuStrip = editableOptionsMenu;

                return editableCombo;

            case TrackingFieldType.LongText:
                var longTextBox = new TextBox
                {
                    Multiline = true,
                    ScrollBars = ScrollBars.Vertical,
                    Height = IsCommentsField(field) ? 66 : 44,
                    PlaceholderText = field.Placeholder ?? string.Empty
                };

                if (IsActivityField(field))
                {
                    longTextBox.TextChanged += ActivityTextBox_TextChanged;
                    longTextBox.KeyDown += ActivityTextBox_KeyDown;
                    longTextBox.Leave += ActivityTextBox_Leave;
                }

                if (IsCommentsField(field))
                {
                    longTextBox.Font = new Font(longTextBox.Font.FontFamily, Math.Max(6F, longTextBox.Font.Size - 1F), longTextBox.Font.Style, GraphicsUnit.Point);
                    longTextBox.KeyDown += CommentsTextBox_KeyDown;
                    longTextBox.KeyPress += CommentsTextBox_KeyPress;
                }

                longTextBox.TextChanged += FieldValueChanged;
                return longTextBox;

            default:
                var textBox = new TextBox
                {
                    Height = 34,
                    PlaceholderText = field.Placeholder ?? string.Empty
                };
                textBox.TextChanged += FieldValueChanged;
                return textBox;
        }
    }

    private Control CreateInputReceivedRow()
    {
        var row = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.FromArgb(249, 251, 252),
            Margin = new Padding(4, 0, 4, 4),
            Padding = new Padding(10, 6, 10, 6)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _inputReceivedPathLabel.Dock = DockStyle.Fill;
        _inputReceivedPathLabel.Height = 36;
        _inputReceivedPathLabel.Font = new Font(
            _inputReceivedPathLabel.Font.FontFamily,
            Math.Max(6F, _inputReceivedPathLabel.Font.Size - 1F),
            _inputReceivedPathLabel.Font.Style,
            GraphicsUnit.Point);
        _inputReceivedPathLabel.TextAlign = ContentAlignment.MiddleLeft;
        _inputReceivedPathLabel.AutoEllipsis = true;
        _inputReceivedPathLabel.Margin = new Padding(0, 4, 8, 0);
        _inputReceivedPathLabel.Click += InputReceivedPathLabel_Click;
        _inputReceivedPathLabel.Resize += InputReceivedPathLabel_Resize;

        const string inputReceivedButtonText = "input recibido";
        ConfigureActionButton(_inputReceivedButton, inputReceivedButtonText, Color.FromArgb(18, 103, 177));
        var minButtonWidth = TextRenderer.MeasureText(inputReceivedButtonText, _inputReceivedButton.Font).Width + 24;
        _inputReceivedButton.AutoSize = false;
        _inputReceivedButton.Width = Math.Max(132, minButtonWidth);
        _inputReceivedButton.Height = 32;
        _inputReceivedButton.Padding = new Padding(8, 0, 8, 0);
        _inputReceivedButton.TextAlign = ContentAlignment.MiddleCenter;
        _inputReceivedButton.Margin = new Padding(0);
        _inputReceivedButton.Click += InputReceivedButton_Click;

        row.Controls.Add(_inputReceivedPathLabel, 0, 0);
        row.Controls.Add(_inputReceivedButton, 1, 0);

        UpdateInputReceivedPathDisplay(null);
        return row;
    }

    private Control CreateUpdatedDateAutoToggle()
    {
        _updatedDateAutoCheckBox.AutoSize = true;
        _updatedDateAutoCheckBox.Text = "Auto hoy";
        _updatedDateAutoCheckBox.Checked = true;
        _updatedDateAutoCheckBox.Margin = new Padding(0, 2, 0, 0);
        _updatedDateAutoCheckBox.CheckedChanged += UpdatedDateAutoCheckBox_CheckedChanged;

        var panel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 22,
            BackColor = Color.Transparent
        };

        _updatedDateAutoCheckBox.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _updatedDateAutoCheckBox.Left = Math.Max(0, panel.Width - _updatedDateAutoCheckBox.PreferredSize.Width - 2);
        panel.Resize += (_, _) =>
        {
            _updatedDateAutoCheckBox.Left = Math.Max(0, panel.Width - _updatedDateAutoCheckBox.PreferredSize.Width - 2);
        };

        panel.Controls.Add(_updatedDateAutoCheckBox);
        return panel;
    }

    private static bool IsActivityField(TrackingFieldDefinition field)
    {
        return string.Equals(field.Key, "activity", StringComparison.OrdinalIgnoreCase) ||
            field.Label.Contains("actividad", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUpdatedDateField(TrackingFieldDefinition field)
    {
        return string.Equals(field.Key, UpdatedDateFieldKey, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdatedDatePicker_ValueChanged(DateTimePicker datePicker)
    {
        if (_suppressEvents || !datePicker.Focused || !_updatedDateAutoCheckBox.Checked)
        {
            return;
        }

        // User manually changed the date: switch to manual mode until Auto hoy is enabled again.
        _suppressUpdatedDateAutoChanged = true;
        _updatedDateAutoCheckBox.Checked = false;
        _suppressUpdatedDateAutoChanged = false;
    }

    private void UpdatedDateAutoCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_suppressEvents || _suppressUpdatedDateAutoChanged)
        {
            return;
        }

        PersistCurrentEntry();
    }

    private void ActivityTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (_suppressEvents)
        {
            return;
        }

        _activityEditedPendingInputPathPrompt = true;
    }

    private void ActivityTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox || e.KeyCode != Keys.Enter)
        {
            return;
        }

        BeginInvoke(() => EnsureInputReceivedPathAfterActivityEdit(textBox));
    }

    private void ActivityTextBox_Leave(object? sender, EventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        EnsureInputReceivedPathAfterActivityEdit(textBox);
    }

    private void EnsureInputReceivedPathAfterActivityEdit(TextBox activityTextBox)
    {
        if (_suppressEvents || _forcingInputReceivedSelection || !_activityEditedPendingInputPathPrompt || _currentEntry is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(activityTextBox.Text))
        {
            _activityEditedPendingInputPathPrompt = false;
            return;
        }

        if (!string.IsNullOrWhiteSpace(_inputReceivedPathLabel.Tag as string))
        {
            _activityEditedPendingInputPathPrompt = false;
            return;
        }

        _forcingInputReceivedSelection = true;

        try
        {
            if (TrySelectInputReceivedPath())
            {
                _activityEditedPendingInputPathPrompt = false;
                return;
            }

            MessageBox.Show(
                this,
                "Debes seleccionar una carpeta en \"input recibido\" para continuar con este registro.",
                "Input recibido requerido",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            BeginInvoke(() =>
            {
                activityTextBox.Focus();
                activityTextBox.SelectionStart = activityTextBox.TextLength;
            });
        }
        finally
        {
            _forcingInputReceivedSelection = false;
        }
    }

    private void InputReceivedButton_Click(object? sender, EventArgs e)
    {
        TrySelectInputReceivedPath();
    }

    private bool TrySelectInputReceivedPath()
    {
        if (_currentEntry is null)
        {
            return false;
        }

        var selectedPath = ShowInputReceivedFolderDialog();

        if (selectedPath is null)
        {
            return false;
        }

        UpdateInputReceivedPathDisplay(selectedPath);
        PersistCurrentEntry();
        _activityEditedPendingInputPathPrompt = false;
        return true;
    }

    private string? ShowInputReceivedFolderDialog()
    {
        var currentPath = _inputReceivedPathLabel.Tag as string;
        var initialPath = ResolveInputReceivedDialogInitialPath(currentPath);

        using var dialog = new FolderBrowserDialog
        {
            Description = "Selecciona la carpeta de input recibido",
            UseDescriptionForTitle = true,
            SelectedPath = initialPath ?? string.Empty
        };

        return dialog.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath)
            ? dialog.SelectedPath
            : null;
    }

    private string? ResolveInputReceivedDialogInitialPath(string? currentPath)
    {
        if (!string.IsNullOrWhiteSpace(currentPath) && Directory.Exists(currentPath))
        {
            return currentPath;
        }

        if (_currentProject is null)
        {
            return null;
        }

        foreach (var entry in _currentProject.Entries.OrderByDescending(item => item.UpdatedAtLocal))
        {
            if (entry.Values.TryGetValue(InputReceivedPathKey, out var entryPath) && !string.IsNullOrWhiteSpace(entryPath) && Directory.Exists(entryPath))
            {
                return entryPath;
            }
        }

        return null;
    }

    private void InputReceivedPathLabel_Click(object? sender, EventArgs e)
    {
        var path = _inputReceivedPathLabel.Tag as string;

        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    private void InputReceivedPathLabel_Resize(object? sender, EventArgs e)
    {
        if (_inputReceivedPathLabel.Tag is string currentPath)
        {
            UpdateInputReceivedPathDisplay(currentPath);
        }
    }

    private static string FormatPathForTwoLinesIfNeeded(string path, Font font, int maxWidth)
    {
        const string continuationIndent = "    ";

        if (string.IsNullOrWhiteSpace(path) || TextRenderer.MeasureText(path, font).Width <= maxWidth)
        {
            return path;
        }

        var fullWidth = TextRenderer.MeasureText(path, font).Width;
        var bestIndex = -1;
        var bestWidth = int.MaxValue;

        for (var index = 1; index < path.Length - 1; index++)
        {
            if (path[index] != '\\' && path[index] != '/')
            {
                continue;
            }

            var firstLine = path[..(index + 1)];
            var secondLine = path[(index + 1)..];
            var indentedSecondLine = continuationIndent + secondLine;
            var maxLineWidth = Math.Max(TextRenderer.MeasureText(firstLine, font).Width, TextRenderer.MeasureText(indentedSecondLine, font).Width);

            if (maxLineWidth < bestWidth)
            {
                bestWidth = maxLineWidth;
                bestIndex = index;
            }
        }

        if (bestIndex <= 0 || bestWidth >= fullWidth)
        {
            return path;
        }

        return path[..(bestIndex + 1)] + Environment.NewLine + continuationIndent + path[(bestIndex + 1)..];
    }

    private void UpdateInputReceivedPathDisplay(string? path)
    {
        var selectedPath = string.IsNullOrWhiteSpace(path) ? null : path.Trim();
        var pathExists = selectedPath is not null && Directory.Exists(selectedPath);

        _inputReceivedPathLabel.Tag = selectedPath;
        _inputReceivedPathLabel.Text = selectedPath is null
            ? "Sin ruta de input recibido"
            : FormatPathForTwoLinesIfNeeded(selectedPath, _inputReceivedPathLabel.Font, Math.Max(120, _inputReceivedPathLabel.Width - 6));
        _inputReceivedPathLabel.ForeColor = selectedPath is null ? Color.FromArgb(132, 140, 152) : Color.FromArgb(22, 58, 92);
        _inputReceivedPathLabel.Cursor = pathExists ? Cursors.Hand : Cursors.Default;

        _toolTip.SetToolTip(
            _inputReceivedPathLabel,
            selectedPath is null
                ? "Selecciona una carpeta con el boton \"input recibido\"."
                : $"{selectedPath}\n(Clic para abrir en el explorador de archivos)");
    }

    private static bool IsCommentsField(TrackingFieldDefinition field)
    {
        return string.Equals(field.Key, "notes", StringComparison.OrdinalIgnoreCase) ||
            field.Label.Contains("coment", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildCommentPrefix()
    {
        return $"[{DateTime.Now:yyyy-MM-dd HH:mm}] ";
    }

    private void CommentsTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox || e.KeyCode != Keys.Enter)
        {
            return;
        }

        textBox.SelectedText = Environment.NewLine + BuildCommentPrefix();
        e.SuppressKeyPress = true;
    }

    private void CommentsTextBox_KeyPress(object? sender, KeyPressEventArgs e)
    {
        if (sender is not TextBox textBox || char.IsControl(e.KeyChar))
        {
            return;
        }

        if (ShouldInsertCommentPrefixAtCaret(textBox))
        {
            textBox.SelectedText = BuildCommentPrefix() + e.KeyChar;
            e.Handled = true;
        }
    }

    private static bool ShouldInsertCommentPrefixAtCaret(TextBox textBox)
    {
        if (textBox.TextLength == 0)
        {
            return true;
        }

        var caretIndex = textBox.SelectionStart;

        if (caretIndex <= 0 || textBox.Text[caretIndex - 1] != '\n')
        {
            return false;
        }

        var nextNewLineIndex = textBox.Text.IndexOf('\n', caretIndex);
        var lineLength = (nextNewLineIndex < 0 ? textBox.Text.Length : nextNewLineIndex) - caretIndex;
        var currentLine = lineLength > 0 ? textBox.Text.Substring(caretIndex, lineLength).TrimStart('\r') : string.Empty;
        return !HasCommentTimestampPrefix(currentLine);
    }

    private static bool HasCommentTimestampPrefix(string text)
    {
        return text.Length >= 19 &&
            text[0] == '[' &&
            text[5] == '-' &&
            text[8] == '-' &&
            text[11] == ' ' &&
            text[14] == ':' &&
            text[17] == ']' &&
            text[18] == ' ';
    }

    private static Panel CreateCardPanel()
    {
        return new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Margin = new Padding(0),
            BorderStyle = BorderStyle.FixedSingle
        };
    }

    // Remembers a value typed into an editable-option field for future entries in this same project (Area, Paquete, etc.).
    private void CommitEditableOptionValue(TrackingFieldDefinition field, ComboBox comboBox)
    {
        if (_suppressEvents || _currentProject is null)
        {
            return;
        }

        var value = comboBox.Text.Trim();

        if (value.Length == 0)
        {
            return;
        }

        if (_workspaceService.RememberFieldValue(_schema, _currentProject, field, value) && !comboBox.Items.Contains(value))
        {
            comboBox.Items.Add(value);
        }
    }

    // Resets every editable-option field's dropdown to this project's values (schema defaults + values learned
    // while working on it), so options typed while on another project don't leak into this one.
    private void RefreshEditableOptionItems(TrackingProject project)
    {
        foreach (var field in _schema.Fields.Where(item => item.Type == TrackingFieldType.EditableOption))
        {
            if (_fieldControls[field.Key] is not ComboBox comboBox)
            {
                continue;
            }

            comboBox.Items.Clear();

            foreach (var option in FieldOptionsHelper.GetOptions(project, field))
            {
                comboBox.Items.Add(option);
            }
        }
    }

    // Right-click menu on an editable-option field listing its current values, each removable on click.
    private void BuildEditableOptionContextMenu(TrackingFieldDefinition field, ComboBox comboBox, ContextMenuStrip menu)
    {
        menu.Items.Clear();

        var options = comboBox.Items.Cast<object>().Select(item => item?.ToString() ?? string.Empty).Where(option => option.Length > 0).ToList();

        if (options.Count == 0)
        {
            menu.Items.Add(new ToolStripMenuItem("No hay valores para eliminar") { Enabled = false });
            return;
        }

        foreach (var option in options)
        {
            var menuItem = new ToolStripMenuItem($"Eliminar \"{option}\"");
            menuItem.Click += (s, e) => DeleteEditableOptionValue(field, comboBox, option);
            menu.Items.Add(menuItem);
        }
    }

    // Removes a value from this field's dropdown (this project's own value and/or the shared schema default).
    private void DeleteEditableOptionValue(TrackingFieldDefinition field, ComboBox comboBox, string value)
    {
        if (_currentProject is null)
        {
            return;
        }

        _workspaceService.ForgetFieldValue(_schema, _currentProject, field, value);
        RefreshEditableOptionItems(_currentProject);

        if (string.Equals(comboBox.Text.Trim(), value, StringComparison.OrdinalIgnoreCase))
        {
            comboBox.Text = string.Empty;
            FieldValueChanged(comboBox, EventArgs.Empty);
        }
    }

    // Clamps this field's date to its minimum (if set), then re-checks any other date field that depends on it.
    private void EnforceDependentMinimumDates(TrackingFieldDefinition field, DateTimePicker datePicker)
    {
        if (_suppressEvents)
        {
            return;
        }

        EnforceMinimumDate(field, datePicker);

        foreach (var dependentField in _schema.Fields.Where(item => item.MinDateFieldKey == field.Key))
        {
            if (_fieldControls.TryGetValue(dependentField.Key, out var dependentControl) && dependentControl is DateTimePicker dependentPicker)
            {
                EnforceMinimumDate(dependentField, dependentPicker);
            }
        }
    }

    private void EnforceMinimumDate(TrackingFieldDefinition field, DateTimePicker datePicker)
    {
        if (field.MinDateFieldKey is null || !datePicker.Checked)
        {
            return;
        }

        if (!_fieldControls.TryGetValue(field.MinDateFieldKey, out var minControl) || minControl is not DateTimePicker minDatePicker || !minDatePicker.Checked)
        {
            return;
        }

        if (datePicker.Value.Date < minDatePicker.Value.Date)
        {
            datePicker.Value = minDatePicker.Value.Date;
        }
    }

    private static void DrawColorCodedComboItem(TrackingFieldDefinition field, ComboBox comboBox, DrawItemEventArgs e)
    {
        e.DrawBackground();

        // The closed/collapsed box repaints with e.Index == -1; fall back to the combo's displayed text.
        var text = e.Index >= 0 ? comboBox.Items[e.Index]?.ToString() ?? string.Empty : comboBox.Text;
        var swatchColor = OptionColorHelper.GetColor(field, text);

        using (var swatchBrush = new SolidBrush(swatchColor))
        {
            var swatchRect = new Rectangle(e.Bounds.Left + 3, e.Bounds.Top + 3, 14, e.Bounds.Height - 6);
            e.Graphics.FillRectangle(swatchBrush, swatchRect);
        }

        var textRect = new Rectangle(e.Bounds.Left + 22, e.Bounds.Top, e.Bounds.Width - 22, e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, text, e.Font ?? comboBox.Font, textRect, e.ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        e.DrawFocusRectangle();
    }

    private void DailyTrackingForm_Load(object? sender, EventArgs e)
    {
        FitWindowToContent();
        RefreshProjectSelector(Guid.Empty);

        var project = _workspaceService.TryOpenLastProject();

        if (project is null)
        {
            project = PromptCreateProject(closeWhenCancelled: true);
        }

        if (project is not null)
        {
            LoadProject(project);
        }
    }

    // Sizes the window tall enough to show every ficha field without vertical scrolling, capped to the screen's working area.
    private void FitWindowToContent()
    {
        // Chrome above/around the dynamic field grid: hero + toolbar + content/editor paddings + heading + scroll top padding.
        const int chromeHeight = 130 + 76 + 18 + 24 + 28 + 8;
        const int buffer = 16;

        var desiredClientHeight = chromeHeight + _editorLayout.PreferredSize.Height + buffer;
        var nonClientHeight = Height - ClientSize.Height;
        var maxClientHeight = Screen.FromControl(this).WorkingArea.Height - nonClientHeight - 20;

        ClientSize = new Size(ClientSize.Width, Math.Max(600, Math.Min(desiredClientHeight, maxClientHeight)));
    }

    private TrackingProject? PromptCreateProject(bool closeWhenCancelled)
    {
        using var dialog = new NewProjectDialog();

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            if (closeWhenCancelled)
            {
                BeginInvoke(Close);
            }

            return null;
        }

        var project = _workspaceService.CreateProject(dialog.ProjectName);
        RefreshProjectSelector(project.ProjectId);
        return project;
    }

    private void LoadProject(TrackingProject project)
    {
        _currentProject = project;
        _hasUnsavedChanges = false;
        RefreshEditableOptionItems(project);

        if (_currentProject.Entries.Count == 0)
        {
            _currentEntry = _workspaceService.CreateEntry(_currentProject, _schema);
            EnsureInitialStatusNote(_currentEntry);
            _hasUnsavedChanges = true;
        }

        RefreshProjectSelector(project.ProjectId);
        RefreshEntriesList();
        UpdateProjectSummary();

        var entryToLoad = _currentEntry ?? _currentProject.Entries.OrderByDescending(entry => entry.UpdatedAtLocal).FirstOrDefault();

        if (entryToLoad is not null)
        {
            LoadEntry(entryToLoad);
        }
    }

    private void LoadEntry(TrackingEntry entry)
    {
        _currentEntry = entry;
        EnsureUpdatedDateAutoFlag(entry);
        _suppressEvents = true;

        foreach (var field in _schema.Fields)
        {
            var value = entry.Values.TryGetValue(field.Key, out var storedValue) ? storedValue : null;
            var control = _fieldControls[field.Key];

            switch (control)
            {
                case TextBox textBox:
                    textBox.Text = value ?? string.Empty;
                    break;

                case ComboBox comboBox:
                    if (comboBox.DropDownStyle == ComboBoxStyle.DropDownList)
                    {
                        comboBox.SelectedItem = value ?? string.Empty;
                        if (comboBox.SelectedIndex < 0)
                        {
                            comboBox.SelectedIndex = 0;
                        }
                    }
                    else
                    {
                        comboBox.Text = value ?? string.Empty;
                    }
                    break;

                case DateTimePicker datePicker:
                    if (DateTime.TryParse(value, out var parsedDate))
                    {
                        datePicker.Value = parsedDate;
                        datePicker.Checked = true;
                    }
                    else
                    {
                        datePicker.Checked = false;
                    }
                    break;
            }
        }

        var inputReceivedPath = entry.Values.TryGetValue(InputReceivedPathKey, out var storedPath) ? storedPath : null;
        UpdateInputReceivedPathDisplay(inputReceivedPath);
        SetUpdatedDateAutoCheckboxState(ReadUpdatedDateAutoFlag(entry));
        _activityEditedPendingInputPathPrompt = false;

        _suppressEvents = false;
        UpdateAutosaveLabel();
        SelectCurrentEntryInList();
    }

    private void PersistCurrentEntry()
    {
        if (_currentProject is null || _currentEntry is null)
        {
            return;
        }

        var previousStatus = GetFieldValue(_currentEntry, StatusFieldKey);

        foreach (var field in _schema.Fields)
        {
            var control = _fieldControls[field.Key];
            var value = ReadControlValue(control);
            _currentEntry.Values[field.Key] = value;

            if (field.DefaultToLastValue &&
                field.Type == TrackingFieldType.Option &&
                !string.IsNullOrWhiteSpace(value))
            {
                _workspaceService.RememberFieldValue(_schema, _currentProject, field, value);
            }
        }

        var updatedDateAutoEnabled = _updatedDateAutoCheckBox.Checked;
        _currentEntry.Values[UpdatedDateAutoKey] = updatedDateAutoEnabled ? "true" : "false";

        if (updatedDateAutoEnabled && !IsUserManuallyEditingUpdatedDate())
        {
            ForceUpdatedDateToToday(_currentEntry);
        }

        var currentStatus = GetFieldValue(_currentEntry, StatusFieldKey);

        if (!string.IsNullOrWhiteSpace(currentStatus) && !string.Equals(previousStatus, currentStatus, StringComparison.OrdinalIgnoreCase))
        {
            AppendNoteEntry(_currentEntry, currentStatus);
        }

        var inputReceivedPath = _inputReceivedPathLabel.Tag as string;
        _currentEntry.Values[InputReceivedPathKey] = string.IsNullOrWhiteSpace(inputReceivedPath) ? null : inputReceivedPath;

        _currentEntry.UpdatedAtLocal = DateTime.Now;
        _hasUnsavedChanges = true;
        RefreshEntriesList();
        UpdateProjectSummary();
        UpdateAutosaveLabel();
    }

    private void EnsureInitialStatusNote(TrackingEntry entry)
    {
        var currentNotes = GetFieldValue(entry, NotesFieldKey);

        if (!string.IsNullOrWhiteSpace(currentNotes))
        {
            return;
        }

        var status = GetFieldValue(entry, StatusFieldKey) ?? "Nuevo";
        AppendNoteEntry(entry, status);
    }

    private void ForceUpdatedDateToToday(TrackingEntry entry)
    {
        var today = DateTime.Today;
        var todayValue = today.ToString("yyyy-MM-dd");
        entry.Values[UpdatedDateFieldKey] = todayValue;

        if (!_fieldControls.TryGetValue(UpdatedDateFieldKey, out var updatedDateControl) || updatedDateControl is not DateTimePicker updatedDatePicker)
        {
            return;
        }

        var previousSuppressState = _suppressEvents;
        _suppressEvents = true;
        updatedDatePicker.Value = today;
        updatedDatePicker.Checked = true;
        _suppressEvents = previousSuppressState;
    }

    private bool IsUserManuallyEditingUpdatedDate()
    {
        if (!_fieldControls.TryGetValue(UpdatedDateFieldKey, out var updatedDateControl) || updatedDateControl is not DateTimePicker updatedDatePicker)
        {
            return false;
        }

        return updatedDatePicker.Focused;
    }

    private static bool ReadUpdatedDateAutoFlag(TrackingEntry entry)
    {
        return entry.Values.TryGetValue(UpdatedDateAutoKey, out var rawValue)
            ? !string.Equals(rawValue, "false", StringComparison.OrdinalIgnoreCase)
            : true;
    }

    private static void EnsureUpdatedDateAutoFlag(TrackingEntry entry)
    {
        if (!entry.Values.ContainsKey(UpdatedDateAutoKey))
        {
            entry.Values[UpdatedDateAutoKey] = "true";
        }
    }

    private void SetUpdatedDateAutoCheckboxState(bool isChecked)
    {
        _suppressUpdatedDateAutoChanged = true;
        _updatedDateAutoCheckBox.Checked = isChecked;
        _suppressUpdatedDateAutoChanged = false;
    }

    private void AppendNoteEntry(TrackingEntry entry, string noteText)
    {
        if (string.IsNullOrWhiteSpace(noteText))
        {
            return;
        }

        var normalizedNoteText = noteText.Trim();
        var currentNotes = GetFieldValue(entry, NotesFieldKey);

        if (IsDuplicateConsecutiveAutoNote(currentNotes, normalizedNoteText))
        {
            return;
        }

        var line = BuildCommentPrefix() + normalizedNoteText;
        var combinedNotes = string.IsNullOrWhiteSpace(currentNotes)
            ? line
            : currentNotes + Environment.NewLine + line;

        entry.Values[NotesFieldKey] = combinedNotes;

        if (!_fieldControls.TryGetValue(NotesFieldKey, out var notesControl) || notesControl is not TextBox notesTextBox)
        {
            return;
        }

        var previousSuppressState = _suppressEvents;
        _suppressEvents = true;
        notesTextBox.Text = combinedNotes;
        notesTextBox.SelectionStart = notesTextBox.TextLength;
        _suppressEvents = previousSuppressState;
    }

    private static bool IsDuplicateConsecutiveAutoNote(string? currentNotes, string nextNoteText)
    {
        if (string.IsNullOrWhiteSpace(currentNotes))
        {
            return false;
        }

        var lines = currentNotes
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (lines.Length == 0)
        {
            return false;
        }

        var lastText = StripCommentPrefix(lines[^1]);
        return string.Equals(lastText, nextNoteText, StringComparison.OrdinalIgnoreCase);
    }

    private static string StripCommentPrefix(string noteLine)
    {
        if (noteLine.Length < 20 || noteLine[0] != '[')
        {
            return noteLine.Trim();
        }

        // Prefix format: [yyyy-MM-dd HH:mm] 
        if (noteLine[5] == '-' && noteLine[8] == '-' && noteLine[11] == ' ' && noteLine[14] == ':' && noteLine[17] == ']' && noteLine[18] == ' ')
        {
            return noteLine[19..].Trim();
        }

        return noteLine.Trim();
    }

    private void SaveCurrentProject()
    {
        if (_currentProject is null)
        {
            return;
        }

        _workspaceService.SaveProject(_currentProject);
        _hasUnsavedChanges = false;
        UpdateAutosaveLabel();
    }

    // If there are unsaved changes, prompts to save/discard/cancel. Returns true when the caller should proceed
    // (switch project, change folder or close the app); false when the user cancelled the action.
    private bool ConfirmUnsavedChangesBeforeProceeding()
    {
        if (!_hasUnsavedChanges)
        {
            return true;
        }

        var result = MessageBox.Show(
            this,
            "Hay cambios sin guardar en el proyecto actual. ¿Deseas guardarlos antes de continuar?",
            "Cambios sin guardar",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Warning);

        switch (result)
        {
            case DialogResult.Yes:
                SaveCurrentProject();
                return true;

            case DialogResult.No:
                _hasUnsavedChanges = false;
                return true;

            default:
                return false;
        }
    }

    private static string? ReadControlValue(Control control)
    {
        return control switch
        {
            TextBox textBox => string.IsNullOrWhiteSpace(textBox.Text) ? null : textBox.Text.Trim(),
            ComboBox comboBox => string.IsNullOrWhiteSpace(comboBox.Text) ? null : comboBox.Text,
            DateTimePicker datePicker => datePicker.Checked ? datePicker.Value.ToString("yyyy-MM-dd") : null,
            _ => null
        };
    }

    private void RefreshProjectSelector(Guid selectedProjectId)
    {
        _suppressEvents = true;
        _projectSelector.Items.Clear();

        foreach (var project in _workspaceService.GetProjects())
        {
            _projectSelector.Items.Add(new ProjectSelectorItem(project.ProjectId, project.ProjectName));
        }

        if (_projectSelector.Items.Count > 0)
        {
            var selectedIndex = 0;

            if (selectedProjectId != Guid.Empty)
            {
                for (var index = 0; index < _projectSelector.Items.Count; index++)
                {
                    if (_projectSelector.Items[index] is ProjectSelectorItem item && item.ProjectId == selectedProjectId)
                    {
                        selectedIndex = index;
                        break;
                    }
                }
            }

            _projectSelector.SelectedIndex = selectedIndex;
        }

        _suppressEvents = false;
    }

    private void RefreshEntriesList()
    {
        _entriesListView.BeginUpdate();
        _entriesListView.Items.Clear();

        foreach (var entry in GetSortedEntries())
        {
            var listItem = new ListViewItem(entry.EntryNumber.ToString())
            {
                Tag = entry.EntryId
            };
            listItem.SubItems.Add(GetEntryDate(entry));
            listItem.SubItems.Add(GetEntryHeadline(entry));
            listItem.SubItems.Add(GetFieldValue(entry, "status") ?? "-");
            _entriesListView.Items.Add(listItem);
        }

        _entriesListView.EndUpdate();
        SelectCurrentEntryInList();
        EntriesListView_Resize(this, EventArgs.Empty);
    }

    private IEnumerable<TrackingEntry> GetSortedEntries()
    {
        if (_currentProject is null)
        {
            return Enumerable.Empty<TrackingEntry>();
        }

        IEnumerable<TrackingEntry> entries = _currentProject.Entries;

        if (!string.IsNullOrEmpty(_statusFilterValue))
        {
            entries = entries.Where(entry => string.Equals(GetFieldValue(entry, "status"), _statusFilterValue, StringComparison.OrdinalIgnoreCase));
        }

        var descending = _sortOrder == SortOrder.Descending;

        return _sortColumnIndex switch
        {
            0 => descending ? entries.OrderByDescending(entry => entry.EntryNumber) : entries.OrderBy(entry => entry.EntryNumber),
            1 => descending ? entries.OrderByDescending(GetSortableDate) : entries.OrderBy(GetSortableDate),
            2 => descending ? entries.OrderByDescending(GetEntryHeadline) : entries.OrderBy(GetEntryHeadline),
            3 => descending ? entries.OrderByDescending(entry => GetFieldValue(entry, "status") ?? string.Empty) : entries.OrderBy(entry => GetFieldValue(entry, "status") ?? string.Empty),
            _ => entries.OrderByDescending(entry => entry.UpdatedAtLocal)
        };
    }

    private DateTime GetSortableDate(TrackingEntry entry)
    {
        var storedDate = GetFieldValue(entry, "recordDate", "date", "fecha");
        return DateTime.TryParse(storedDate, out var parsedDate) ? parsedDate : entry.UpdatedAtLocal;
    }

    private void SelectCurrentEntryInList()
    {
        if (_currentEntry is null)
        {
            return;
        }

        // Setting Selected re-fires SelectedIndexChanged even for an unchanged state,
        // so suppress it here to avoid re-entering LoadEntry and looping infinitely.
        var previousSuppressState = _suppressEvents;
        _suppressEvents = true;

        foreach (ListViewItem item in _entriesListView.Items)
        {
            item.Selected = item.Tag is Guid entryId && entryId == _currentEntry.EntryId;
        }

        _suppressEvents = previousSuppressState;
    }

    private string GetEntryDate(TrackingEntry entry)
    {
        var storedDate = GetFieldValue(entry, "recordDate", "date", "fecha");

        if (DateTime.TryParse(storedDate, out var parsedDate))
        {
            return parsedDate.ToString("dd/MM/yyyy");
        }

        return entry.UpdatedAtLocal.ToString("dd/MM/yyyy");
    }

    private string GetEntryHeadline(TrackingEntry entry)
    {
        return GetFieldValue(entry, "activity", "descripcion", "title")
            ?? GetFieldValue(entry, "area", "package")
            ?? "Registro sin titulo";
    }

    private static string? GetFieldValue(TrackingEntry entry, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (entry.Values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private void UpdateProjectSummary()
    {
        _viewChartButton.Enabled = _currentProject is not null;
        _createReportButton.Enabled = _currentProject is not null;
        _manageProjectUsersButton.Enabled = _currentProject is not null;

        if (_currentProject is null)
        {
            _projectSummaryLabel.Text = "Sin proyecto cargado";
            return;
        }

        _projectSummaryLabel.Text = $"Proyecto activo: {_currentProject.ProjectName}  |  Registros: {_currentProject.Entries.Count}";
    }

    // Shows the full folder path as a tooltip and as a label under the "Carpeta de proyectos" button, sizing the
    // panel to the longer of the button/path text so the path is never cut off (falls back to ellipsis only if it
    // would not fit next to the title area at all).
    private void UpdateProjectsFolderDisplay()
    {
        var path = _workspaceService.GetProjectsFolderPath();
        var displayedPath = FormatPathForTwoLines(path);
        _toolTip.SetToolTip(_chooseProjectsFolderButton, path);
        _projectsFolderPathLabel.Text = displayedPath;
        _toolTip.SetToolTip(_projectsFolderPathLabel, $"{path}\n(Clic para abrir en el explorador de archivos)");

        var folderButtonSize = _chooseProjectsFolderButton.GetPreferredSize(Size.Empty);
        var displayedLines = displayedPath.Split(Environment.NewLine);
        var pathTextWidth = displayedLines.Max(line => TextRenderer.MeasureText(line, _projectsFolderPathLabel.Font).Width);
        var desiredWidth = Math.Max(folderButtonSize.Width, pathTextWidth) + 16;
        var maxWidth = Math.Max(240, ClientSize.Width - 650);

        _folderButtonPanel.Width = Math.Min(Math.Max(desiredWidth, 240), maxWidth);
        _chooseProjectsFolderButton.Left = _folderButtonPanel.Width - folderButtonSize.Width - 8;
        _projectsFolderPathLabel.Left = 0;
        _projectsFolderPathLabel.Width = _folderButtonPanel.Width;
    }

    private static string FormatPathForTwoLines(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length < 56)
        {
            return path;
        }

        var midpoint = path.Length / 2;
        var splitIndex = path.LastIndexOf('\\', midpoint);

        if (splitIndex < 0)
        {
            splitIndex = path.LastIndexOf('/', midpoint);
        }

        if (splitIndex <= 0 || splitIndex >= path.Length - 1)
        {
            return path;
        }

        return path[..splitIndex] + Environment.NewLine + path[(splitIndex + 1)..];
    }

    private void UpdateAutosaveLabel()
    {
        _deleteEntryButton.Enabled = _currentEntry is not null;

        if (_currentEntry is null)
        {
            UpdateInputReceivedPathDisplay(null);
            _activityEditedPendingInputPathPrompt = false;
            _autosaveLabel.Text = "Sin registro seleccionado";
            return;
        }

        _autosaveLabel.Text = _hasUnsavedChanges
            ? "Cambios sin guardar"
            : $"Guardado: {_currentEntry.UpdatedAtLocal:dd/MM/yyyy HH:mm:ss}";
    }

    private void FieldValueChanged(object? sender, EventArgs e)
    {
        if (_suppressEvents)
        {
            return;
        }

        PersistCurrentEntry();
    }

    private void ProjectSelector_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_suppressEvents || _projectSelector.SelectedItem is not ProjectSelectorItem item)
        {
            return;
        }

        if (item.ProjectId == _currentProject?.ProjectId)
        {
            return;
        }

        if (!ConfirmUnsavedChangesBeforeProceeding())
        {
            RefreshProjectSelector(_currentProject?.ProjectId ?? Guid.Empty);
            return;
        }

        var project = _workspaceService.OpenProject(item.ProjectId);

        if (project is not null)
        {
            _currentEntry = null;
            LoadProject(project);
        }
    }

    private void NewProjectButton_Click(object? sender, EventArgs e)
    {
        if (!ConfirmUnsavedChangesBeforeProceeding())
        {
            return;
        }

        var project = PromptCreateProject(closeWhenCancelled: false);

        if (project is not null)
        {
            _currentEntry = null;
            LoadProject(project);
        }
    }

    // Deletes the project chosen in the picker immediately (no extra confirmation), then loads another
    // project (or prompts to create one) if the deleted project was the one currently loaded.
    private void DeleteProjectButton_Click(object? sender, EventArgs e)
    {
        var projects = _workspaceService.GetProjects();

        if (projects.Count == 0)
        {
            MessageBox.Show(this, "No hay proyectos para eliminar.", "Eliminar proyecto", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new DeleteProjectDialog(projects);

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var projectToDelete = projects.FirstOrDefault(project => project.ProjectId == dialog.SelectedProjectId);

        if (projectToDelete is null)
        {
            return;
        }

        var wasCurrentProject = projectToDelete.ProjectId == _currentProject?.ProjectId;
        _workspaceService.DeleteProject(projectToDelete);

        if (!wasCurrentProject)
        {
            RefreshProjectSelector(_currentProject?.ProjectId ?? Guid.Empty);
            return;
        }

        _hasUnsavedChanges = false;
        _currentProject = null;
        _currentEntry = null;

        var nextProject = _workspaceService.GetProjects().FirstOrDefault() ?? PromptCreateProject(closeWhenCancelled: false);

        if (nextProject is not null)
        {
            LoadProject(nextProject);
        }
        else
        {
            RefreshProjectSelector(Guid.Empty);
            UpdateProjectSummary();
            UpdateAutosaveLabel();
        }
    }

    private void ManageProjectUsersButton_Click(object? sender, EventArgs e)
    {
        if (_currentProject is null)
        {
            MessageBox.Show(this, "Selecciona o crea un proyecto primero.", "Usuarios del proyecto", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new ProjectUsersDialog(_currentProject.ProjectName, _currentProject.AllowedUserNames);

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _workspaceService.UpdateAllowedUsers(_currentProject, dialog.Users);
    }

    private void NewEntryButton_Click(object? sender, EventArgs e)
    {
        if (_currentProject is null)
        {
            return;
        }

        var entry = _workspaceService.CreateEntry(_currentProject, _schema);
        EnsureInitialStatusNote(entry);
        LoadProject(_currentProject);
        _hasUnsavedChanges = true;
        LoadEntry(entry);
    }

    private void DeleteEntryButton_Click(object? sender, EventArgs e)
    {
        if (_currentProject is null || _currentEntry is null)
        {
            return;
        }

        var confirmResult = MessageBox.Show(
            this,
            "Se eliminara este registro de forma permanente. Deseas continuar?",
            "Confirmar eliminacion",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (confirmResult != DialogResult.Yes)
        {
            return;
        }

        _workspaceService.DeleteEntry(_currentProject, _currentEntry);
        _currentEntry = null;
        LoadProject(_currentProject);
        _hasUnsavedChanges = true;
        UpdateAutosaveLabel();
    }

    private void ViewChartButton_Click(object? sender, EventArgs e)
    {
        if (_currentProject is null)
        {
            return;
        }

        using var chartForm = new TrackingChartForm(_currentProject, _schema);
        chartForm.ShowDialog(this);
    }

    private void CreateReportButton_Click(object? sender, EventArgs e)
    {
        if (_currentProject is null)
        {
            return;
        }

        using var reportForm = new TrackingReportForm(_currentProject, _schema);
        reportForm.ShowDialog(this);
    }

    // Opens the projects folder in File Explorer when the user clicks its path label.
    private void ProjectsFolderPathLabel_Click(object? sender, EventArgs e)
    {
        var path = _workspaceService.GetProjectsFolderPath();

        if (!Directory.Exists(path))
        {
            MessageBox.Show(this, "La carpeta de proyectos no existe o no esta disponible.", "Carpeta de proyectos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    private void ChooseProjectsFolderButton_Click(object? sender, EventArgs e)
    {
        if (!ConfirmUnsavedChangesBeforeProceeding())
        {
            return;
        }

        var currentFolderPath = _workspaceService.GetProjectsFolderPath();

        using var dialog = new FolderBrowserDialog
        {
            Description = "Selecciona la carpeta compartida donde se guardaran los proyectos",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(currentFolderPath) ? currentFolderPath : string.Empty
        };

        if (dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            return;
        }

        var comparison = _workspaceService.CompareProjectsFolder(dialog.SelectedPath);

        // Only projects that exist on both sides with different content require a user decision;
        // projects that only exist on one side are merged automatically without prompting.
        foreach (var conflict in comparison.Conflicts)
        {
            using var conflictDialog = new ProjectVersionConflictDialog(
                conflict.AppVersion.ProjectName,
                conflict.AppVersion.UpdatedAtLocal,
                conflict.FolderVersion.UpdatedAtLocal);

            conflictDialog.ShowDialog(this);

            switch (conflictDialog.Choice)
            {
                case ProjectVersionConflictDialog.ConflictChoice.UseAppVersion:
                    comparison.ProjectsToKeep.Add(conflict.AppVersion);
                    break;

                case ProjectVersionConflictDialog.ConflictChoice.UseFolderVersion:
                    comparison.ProjectsToKeep.Add(conflict.FolderVersion);
                    break;

                default:
                    _workspaceService.LogFolderChangeDecision(comparison, accepted: false);
                    return;
            }
        }

        _workspaceService.ApplyProjectsFolderChange(comparison);
        _workspaceService.LogFolderChangeDecision(comparison, accepted: true);
        UpdateProjectsFolderDisplay();

        var previousProjectId = _currentProject?.ProjectId;
        var projects = _workspaceService.GetProjects();
        var projectToLoad = projects.FirstOrDefault(project => project.ProjectId == previousProjectId) ?? projects.FirstOrDefault();

        _currentEntry = null;

        if (projectToLoad is not null)
        {
            LoadProject(projectToLoad);
        }
        else
        {
            _currentProject = null;
            RefreshProjectSelector(Guid.Empty);
            RefreshEntriesList();
            UpdateProjectSummary();
            UpdateAutosaveLabel();
        }

        MessageBox.Show(
            this,
            $"Carpeta de proyectos actualizada:\n{dialog.SelectedPath}",
            "Carpeta de proyectos",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void DailyTrackingForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!ConfirmUnsavedChangesBeforeProceeding())
        {
            e.Cancel = true;
            return;
        }

        _workspaceService.CreateExitBackup(_currentProject);
    }

    private void EntriesListView_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_suppressEvents || _currentProject is null || _entriesListView.SelectedItems.Count == 0)
        {
            return;
        }

        if (_entriesListView.SelectedItems[0].Tag is Guid entryId)
        {
            var entry = _currentProject.Entries.FirstOrDefault(item => item.EntryId == entryId);

            if (entry is not null)
            {
                LoadEntry(entry);
            }
        }
    }

    private void EntriesListView_Resize(object? sender, EventArgs e)
    {
        if (_entriesListView.Columns.Count != 4)
        {
            return;
        }

        var availableWidth = Math.Max(_entriesListView.ClientSize.Width - 8, 260);
        _entriesListView.Columns[0].Width = 50;
        _entriesListView.Columns[1].Width = 95;
        _entriesListView.Columns[3].Width = 100;
        _entriesListView.Columns[2].Width = Math.Max(availableWidth - 245, 120);
    }

    private void EntriesListView_ColumnClick(object? sender, ColumnClickEventArgs e)
    {
        if (_sortColumnIndex == e.Column)
        {
            _sortOrder = _sortOrder == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
        }
        else
        {
            _sortColumnIndex = e.Column;
            _sortOrder = SortOrder.Ascending;
        }

        RefreshEntriesList();
    }

    private void StatusFilterCombo_SelectedIndexChanged(object? sender, EventArgs e)
    {
        _statusFilterValue = _statusFilterCombo.SelectedIndex <= 0 ? null : _statusFilterCombo.Text;
        RefreshEntriesList();
    }

    private void EntriesListView_DrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
    {
        const int statusColumnIndex = 3;

        if (e.ColumnIndex != statusColumnIndex || _statusFieldDefinition is null || e.Item is null || e.SubItem is null)
        {
            e.DrawDefault = true;
            return;
        }

        var backColor = e.Item.Selected ? SystemColors.Highlight : Color.White;

        using (var backBrush = new SolidBrush(backColor))
        {
            e.Graphics.FillRectangle(backBrush, e.Bounds);
        }

        var swatchColor = OptionColorHelper.GetColor(_statusFieldDefinition, e.SubItem.Text);
        var swatchRect = new Rectangle(e.Bounds.Left + 4, e.Bounds.Top + (e.Bounds.Height - 14) / 2, 14, 14);

        using (var swatchBrush = new SolidBrush(swatchColor))
        {
            e.Graphics.FillRectangle(swatchBrush, swatchRect);
        }

        e.Graphics.DrawRectangle(Pens.Gray, swatchRect);

        var textColor = e.Item.Selected ? SystemColors.HighlightText : SystemColors.WindowText;
        var textRect = new Rectangle(swatchRect.Right + 6, e.Bounds.Top, e.Bounds.Right - swatchRect.Right - 6, e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, e.SubItem.Text, _entriesListView.Font, textRect, textColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
    }

    private sealed class ProjectSelectorItem
    {
        public ProjectSelectorItem(Guid projectId, string projectName)
        {
            ProjectId = projectId;
            ProjectName = projectName;
        }

        public Guid ProjectId { get; }

        public string ProjectName { get; }

        public override string ToString()
        {
            return ProjectName;
        }
    }
}