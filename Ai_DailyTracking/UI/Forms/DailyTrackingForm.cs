using Ai_DailyTracking.Application.Services;
using Ai_DailyTracking.Domain.Models;
using Ai_DailyTracking.Infrastructure;
using Ai_DailyTracking.Shared.Helpers;
using Ai_DailyTracking.UI.Controls;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ai_DailyTracking.UI.Forms;

public sealed class DailyTrackingForm : Form
{
    private const string InputReceivedPathKey = "inputReceivedPath";
    private const string MarkdownFilePathKey = "markdownFilePath";
    private const string NotesFieldKey = "notes";
    private const string StatusFieldKey = "status";
    private const string PriorityFieldKey = "priority";
    private const string UpdatedDateFieldKey = "updatedDate";
    private const string UpdatedDateAutoKey = "updatedDateAuto";
    private static readonly IReadOnlyDictionary<string, int> StatusRanks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["Nuevo"] = 1,
        ["En curso"] = 2,
        ["En revisión"] = 3,
        ["Hecho"] = 4,
        ["Cerrado"] = 5
    };
    private static readonly IReadOnlyDictionary<string, string> PriorityIndicators = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Baja"] = "↓",
        ["Media"] = "—",
        ["Alta"] = "↑",
        ["Critica"] = "!",
        ["Crítica"] = "!"
    };
    private static readonly IReadOnlyDictionary<string, int> PriorityRanks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["Baja"] = 1,
        ["Media"] = 2,
        ["Alta"] = 3,
        ["Critica"] = 4,
        ["Crítica"] = 4
    };
    private static readonly Regex NoteTimestampRegex = new(
        @"\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex NoteStatusRegex = new(
        @"\*\*(?<status>[^*\r\n]+)\*\*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ProjectWorkspaceService _workspaceService;
    private readonly TrackingEntryMarkdownExporter _markdownExporter;
    private readonly SharedPathService _sharedPathService;
    private readonly TrackingFormSchema _schema;
    private readonly Dictionary<string, Control> _fieldControls = new(StringComparer.OrdinalIgnoreCase);
    private readonly ComboBox _projectSelector = new();
    private readonly FlowLayoutPanel _projectSelectorRow = new();
    private readonly Button _retryProjectButton = new();
    private readonly Button _newProjectButton = new();
    private readonly Button _deleteProjectButton = new();
    private readonly Button _manageProjectUsersButton = new();
    private readonly Button _newEntryButton = new();
    private readonly Button _saveEntryButton = new();
    private readonly Button _deleteEntryButton = new();
    private readonly Button _viewChartButton = new();
    private readonly Button _createReportButton = new();
    private readonly Button _chooseProjectsFolderButton = new();
    private readonly Panel _folderButtonPanel = new();
    private readonly Label _projectSummaryLabel = new();
    private readonly Label _autosaveLabel = new();
    private readonly Label _projectsFolderPathLabel = new();
    private readonly SingleLinePathLabel _inputReceivedPathLabel = new();
    private readonly Button _inputReceivedButton = new();
    private readonly SingleLinePathLabel _markdownFilePathLabel = new();
    private readonly Button _createMarkdownButton = new();
    private readonly Button _updateMarkdownButton = new();
    private readonly CheckBox _updatedDateAutoCheckBox = new();
    private readonly ListView _entriesListView = new();
    private readonly ImageList _entriesListImageList = new();
    private readonly MultiSelectFilterDropDown _statusFilterDropDown;
    private readonly MultiSelectFilterDropDown _priorityFilterDropDown;
    private readonly MultiSelectFilterDropDown _assignedToFilterDropDown;
    private readonly TableLayoutPanel _editorLayout = new();
    private readonly ToolTip _toolTip = new();
    private readonly System.Windows.Forms.Timer _presenceTimer = new() { Interval = 60_000 };
    private readonly HashSet<Guid> _announcedEditorSessions = [];
    private readonly TrackingFieldDefinition? _statusFieldDefinition;
    private readonly TrackingFieldDefinition? _priorityFieldDefinition;

    private bool _suppressEvents;
    private bool _suppressUpdatedDateAutoChanged;
    private bool _hasUnsavedChanges;
    private bool _pendingLongTextChanges;
    private int _pendingConflictCount;
    private TrackingProject? _currentProject;
    private TrackingEntry? _currentEntry;
    private int _sortColumnIndex = 2;
    private SortOrder _sortOrder = SortOrder.Ascending;

    public DailyTrackingForm(ProjectWorkspaceService workspaceService, TrackingEntryMarkdownExporter markdownExporter, SharedPathService sharedPathService)
    {
        _workspaceService = workspaceService;
        _markdownExporter = markdownExporter;
        _sharedPathService = sharedPathService;
        _schema = _workspaceService.LoadSchema();
        _statusFieldDefinition = _schema.Fields.FirstOrDefault(field => string.Equals(field.Key, "status", StringComparison.OrdinalIgnoreCase));
        _priorityFieldDefinition = _schema.Fields.FirstOrDefault(field => string.Equals(field.Key, "priority", StringComparison.OrdinalIgnoreCase));
        _statusFilterDropDown = new MultiSelectFilterDropDown("Estado", BuildOptionColorResolver(_statusFieldDefinition));
        _priorityFilterDropDown = new MultiSelectFilterDropDown("Prioridad", BuildOptionColorResolver(_priorityFieldDefinition));
        _assignedToFilterDropDown = new MultiSelectFilterDropDown("Asignado a");

        Text = "Seguimiento diario de proyectos";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1500, 700);
        Size = new Size(1500, 900);
        BackColor = Color.FromArgb(236, 240, 244);
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        ApplyExecutableIcon();

        BuildLayout();
        BuildDynamicEditor();
        UpdateAutosaveLabel();

        Load += DailyTrackingForm_Load;
        FormClosing += DailyTrackingForm_FormClosing;
        _presenceTimer.Tick += (_, _) =>
        {
            if (_currentProject is null) return;
            _workspaceService.RefreshPresence();
            ShowOtherEditorsNotice();
        };
        _presenceTimer.Start();
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
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 160F));
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
        _projectsFolderPathLabel.Height = 76;
        _projectsFolderPathLabel.TextAlign = ContentAlignment.TopRight;
        _projectsFolderPathLabel.AutoEllipsis = false;
        _projectsFolderPathLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point);
        _projectsFolderPathLabel.ForeColor = Color.FromArgb(190, 202, 214);
        _projectsFolderPathLabel.Cursor = Cursors.Hand;
        _projectsFolderPathLabel.Click += ProjectsFolderPathLabel_Click;

        _folderButtonPanel.Controls.Add(_chooseProjectsFolderButton);
        _folderButtonPanel.Controls.Add(_projectsFolderPathLabel);

        var titleLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 38,
            Text = "Seguimiento Diario de Tareas de Proyecto",
            Font = new Font("Segoe UI Semibold", 22F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = Color.White
        };

        _projectSelectorRow.Dock = DockStyle.Top;
        _projectSelectorRow.AutoSize = true;
        _projectSelectorRow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _projectSelectorRow.FlowDirection = FlowDirection.LeftToRight;
        _projectSelectorRow.WrapContents = false;
        _projectSelectorRow.Margin = new Padding(0, 12, 0, 0);
        _projectSelectorRow.Padding = new Padding(0, 5, 0, 0);

        _projectSelector.Width = 260;
        _projectSelector.Height = 30;
        _projectSelector.Margin = new Padding(0, 17, 16, 0);
        _projectSelector.DropDownStyle = ComboBoxStyle.DropDownList;
        _projectSelector.SelectedIndexChanged += ProjectSelector_SelectedIndexChanged;

        ConfigureActionButton(_retryProjectButton, "Fusionar", Color.FromArgb(18, 103, 177), larger: true);
        _retryProjectButton.Margin = new Padding(0, 10, 8, 0);
        _retryProjectButton.Click += RefreshCurrentProjectButton_Click;

        ConfigureHeaderProjectButton(_manageProjectUsersButton, "Usuarios del\r\nproyecto", Color.FromArgb(96, 100, 112), 148);
        _manageProjectUsersButton.Margin = new Padding(0, 10, 8, 0);
        _manageProjectUsersButton.Click += ManageProjectUsersButton_Click;

        ConfigureHeaderProjectButton(_newProjectButton, "Nuevo\r\nproyecto", Color.FromArgb(18, 103, 177), 128);
        _newProjectButton.Margin = new Padding(0, 10, 8, 0);
        _newProjectButton.Click += NewProjectButton_Click;

        ConfigureHeaderProjectButton(_deleteProjectButton, "Eliminar\r\nproyecto", Color.FromArgb(178, 34, 52), 148);
        _deleteProjectButton.Margin = new Padding(0, 10, 8, 0);
        _deleteProjectButton.Click += DeleteProjectButton_Click;

        _projectSelectorRow.Controls.Add(_projectSelector);
        _projectSelectorRow.Controls.Add(_retryProjectButton);
        _projectSelectorRow.Controls.Add(_manageProjectUsersButton);
        _projectSelectorRow.Controls.Add(_newProjectButton);
        _projectSelectorRow.Controls.Add(_deleteProjectButton);

        var textPanel = new Panel { Dock = DockStyle.Fill };
        textPanel.Controls.Add(_projectSelectorRow);
        textPanel.Controls.Add(titleLabel);

        panel.Controls.Add(textPanel);
        panel.Controls.Add(_folderButtonPanel);
        panel.Resize += (_, _) => UpdateProjectsFolderDisplay();
        UpdateProjectsFolderDisplay();
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

        ConfigureActionButton(_saveEntryButton, "Guardar registro", Color.FromArgb(96, 100, 112));
        _saveEntryButton.Click += (_, _) => SaveCurrentProject();

        ConfigureActionButton(_deleteEntryButton, "Eliminar registro", Color.FromArgb(178, 34, 52));
        _deleteEntryButton.Click += DeleteEntryButton_Click;

        ConfigureActionButton(_viewChartButton, "Ver grafico", Color.FromArgb(46, 139, 87));
        _viewChartButton.Click += ViewChartButton_Click;

        ConfigureActionButton(_createReportButton, "Crear informe", Color.FromArgb(96, 74, 158));
        _createReportButton.Click += CreateReportButton_Click;

        buttonsFlow.Controls.Add(_newEntryButton);
        buttonsFlow.Controls.Add(_saveEntryButton);
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
            MinExtra = 620
        };

        var entriesPanel = CreateEntriesPanel();
        entriesPanel.Dock = DockStyle.Left;
        entriesPanel.Width = 620;

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

        if (_statusFieldDefinition is not null)
        {
            _statusFilterDropDown.SetOptions(_statusFieldDefinition.Options);
        }

        _statusFilterDropDown.SelectionChanged += (s, e) => RefreshEntriesList();

        if (_priorityFieldDefinition is not null)
        {
            _priorityFilterDropDown.SetOptions(_priorityFieldDefinition.Options);
        }

        _priorityFilterDropDown.SelectionChanged += (s, e) => RefreshEntriesList();
        _assignedToFilterDropDown.SelectionChanged += (s, e) => RefreshEntriesList();

        filterRow.Controls.Add(CreateFilterGroup("Estado", _statusFilterDropDown));
        filterRow.Controls.Add(CreateFilterGroup("Prioridad", _priorityFilterDropDown));
        filterRow.Controls.Add(CreateFilterGroup("Asignado a", _assignedToFilterDropDown));

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
        _entriesListImageList.ImageSize = new Size(1, 30);
        _entriesListView.SmallImageList = _entriesListImageList;
        _entriesListView.OwnerDraw = true;
        _entriesListView.Columns.Add(string.Empty, 36).TextAlign = HorizontalAlignment.Center;
        _entriesListView.Columns.Add(string.Empty, 30).TextAlign = HorizontalAlignment.Center;
        _entriesListView.Columns.Add("ID", 50);
        _entriesListView.Columns.Add("Fecha", 95);
        _entriesListView.Columns.Add("Disciplina", 130);
        _entriesListView.Columns.Add("Area", 130);
        _entriesListView.Columns.Add("Paquete", 130);
        _entriesListView.SelectedIndexChanged += EntriesListView_SelectedIndexChanged;
        _entriesListView.Resize += EntriesListView_Resize;
        _entriesListView.ColumnClick += EntriesListView_ColumnClick;
        _entriesListView.DrawColumnHeader += EntriesListView_DrawColumnHeader;
        _entriesListView.DrawItem += EntriesListView_DrawItem;
        _entriesListView.DrawSubItem += EntriesListView_DrawSubItem;

        card.Controls.Add(_entriesListView);
        card.Controls.Add(headerPanel);
        return card;
    }

    private static Control CreateFilterGroup(string title, Control filter)
    {
        var group = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0, 0, 12, 0)
        };
        group.Controls.Add(new Label { Text = title, AutoSize = true, Margin = new Padding(0, 0, 0, 2) });
        filter.Margin = Padding.Empty;
        group.Controls.Add(filter);
        return group;
    }

    private static void ConfigureHeaderProjectButton(Button button, string text, Color backColor, int width)
    {
        ConfigureActionButton(button, text, backColor);
        button.AutoSize = false;
        button.Size = new Size(width, 48);
        button.Padding = new Padding(8, 2, 8, 2);
        button.UseCompatibleTextRendering = true;
        button.TextAlign = ContentAlignment.MiddleCenter;
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

                if (IsCommentsField(field))
                {
                    AddEditorRow(CreateMarkdownRow());
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
        editor.Tag = field.Key;

        if (IsUpdatedDateField(field))
        {
            card.Controls.Add(CreateUpdatedDateAutoToggle());
        }

        if (field.Type == TrackingFieldType.LongText)
        {
            // Fill-docked controls must be added before their Top/Bottom siblings, otherwise the
            // fill control claims the whole card and hides the label/grip behind it.
            editor.Dock = DockStyle.Fill;
            card.Controls.Add(editor);
            card.Controls.Add(CreateFieldCardResizeGrip(card, GetFieldCardHeight(field)));
        }
        else
        {
            editor.Dock = DockStyle.Bottom;
            card.Controls.Add(editor);
        }

        card.Controls.Add(label);
        _fieldControls[field.Key] = editor;
        return card;
    }

    private Control CreateFieldCardResizeGrip(Panel card, int minimumHeight)
    {
        var grip = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 6,
            Cursor = Cursors.SizeNS,
            BackColor = Color.FromArgb(221, 226, 230)
        };
        _toolTip.SetToolTip(grip, "Arrastra hacia abajo para agrandar el campo");

        var dragging = false;
        var dragStartScreenY = 0;
        var dragStartRowHeight = 0;

        grip.MouseDown += (s, e) =>
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            dragging = true;
            dragStartScreenY = Cursor.Position.Y;
            dragStartRowHeight = card.Height;
        };

        grip.MouseMove += (s, e) =>
        {
            if (!dragging || card.Parent is not TableLayoutPanel layout)
            {
                return;
            }

            var rowIndex = layout.GetRow(card);
            var delta = Cursor.Position.Y - dragStartScreenY;
            var newHeight = Math.Max(minimumHeight, dragStartRowHeight + delta);
            layout.RowStyles[rowIndex].SizeType = SizeType.Absolute;
            layout.RowStyles[rowIndex].Height = newHeight;
            layout.PerformLayout();
        };

        grip.MouseUp += (s, e) => dragging = false;

        return grip;
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
                if (IsCommentsField(field))
                {
                    var notesTextBox = new RichTextBox
                    {
                        Multiline = true,
                        ScrollBars = RichTextBoxScrollBars.Vertical,
                        Height = 66,
                        DetectUrls = false,
                        // Notes used to be one point smaller than the control default.
                        // Keeping the default size makes them exactly one point larger.
                        Font = new Font(Control.DefaultFont.FontFamily, Control.DefaultFont.Size, FontStyle.Regular, GraphicsUnit.Point)
                    };
                    notesTextBox.KeyDown += CommentsTextBox_KeyDown;
                    notesTextBox.KeyPress += CommentsTextBox_KeyPress;
                    // Colors are recalculated only when the field loses focus, not on every keystroke.
                    notesTextBox.Leave += (s, e) =>
                    {
                        ApplyNotesFormatting(notesTextBox);
                        CommitPendingLongTextChanges();
                    };
                    notesTextBox.TextChanged += LongTextValueChanged;
                    return notesTextBox;
                }

                var longTextBox = new TextBox
                {
                    Multiline = true,
                    ScrollBars = ScrollBars.Vertical,
                    Height = 44,
                    PlaceholderText = field.Placeholder ?? string.Empty
                };

                if (IsActivityField(field))
                {
                    longTextBox.TextChanged += LongTextValueChanged;
                    longTextBox.Leave += (s, e) => CommitPendingLongTextChanges();
                }
                else
                {
                    longTextBox.TextChanged += FieldValueChanged;
                }
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
        var row = new Panel
        {
            Dock = DockStyle.Top,
            BackColor = Color.Transparent,
            Margin = new Padding(4, 0, 4, 4),
            Padding = new Padding(0),
            Height = 44,
            MinimumSize = new Size(0, 44)
        };

        var inputPanel = CreateInputReceivedPanel();
        inputPanel.Dock = DockStyle.Fill;
        row.Controls.Add(inputPanel);

        UpdateInputReceivedPathDisplay(null);
        return row;
    }

    private Control CreateMarkdownRow()
    {
        var row = new Panel
        {
            Dock = DockStyle.Top,
            BackColor = Color.Transparent,
            Margin = new Padding(4, 0, 4, 4),
            Padding = new Padding(0),
            Height = 44,
            MinimumSize = new Size(0, 44)
        };

        var markdownPanel = CreateMarkdownPanel();
        markdownPanel.Dock = DockStyle.Fill;
        row.Controls.Add(markdownPanel);

        UpdateMarkdownFilePathDisplay(null);
        return row;
    }

    private Control CreateInputReceivedPanel()
    {
        var panel = CreatePathActionPanel();

        ConfigurePathLabel(_inputReceivedPathLabel);
        _inputReceivedPathLabel.MouseClick += InputReceivedPathLabel_Click;
        _inputReceivedPathLabel.Resize += InputReceivedPathLabel_Resize;
        _inputReceivedPathLabel.MouseUp += InputReceivedPathLabel_MouseUp;

        ConfigureActionButtonWithFixedWidth(_inputReceivedButton, "Carpeta Documentación", Color.FromArgb(18, 103, 177), 156);
        _inputReceivedButton.Click += InputReceivedButton_Click;

        panel.Controls.Add(_inputReceivedButton, 0, 0);
        panel.Controls.Add(_inputReceivedPathLabel, 1, 0);
        return panel;
    }

    private Control CreateMarkdownPanel()
    {
        var panel = CreatePathActionPanel(2);

        ConfigurePathLabel(_markdownFilePathLabel);
        _markdownFilePathLabel.MouseClick += MarkdownFilePathLabel_Click;
        _markdownFilePathLabel.Resize += MarkdownFilePathLabel_Resize;
        _markdownFilePathLabel.MouseUp += MarkdownFilePathLabel_MouseUp;

        ConfigureActionButtonWithFixedWidth(_createMarkdownButton, "Crear MD", Color.FromArgb(46, 139, 87), 104);
        _createMarkdownButton.Click += CreateMarkdownButton_Click;

        ConfigureActionButtonWithFixedWidth(_updateMarkdownButton, "Actualizar MD", Color.FromArgb(34, 111, 168), 116);
        _updateMarkdownButton.Click += UpdateMarkdownButton_Click;
        _updateMarkdownButton.Margin = new Padding(6, 0, 0, 0);

        panel.Controls.Add(_createMarkdownButton, 0, 0);
        panel.Controls.Add(_updateMarkdownButton, 1, 0);
        panel.Controls.Add(_markdownFilePathLabel, 2, 0);
        return panel;
    }

    private static TableLayoutPanel CreatePathActionPanel(int actionButtonCount = 1)
    {
        var panel = new TableLayoutPanel
        {
            AutoSize = false,
            ColumnCount = actionButtonCount + 1,
            RowCount = 1,
            BackColor = Color.FromArgb(249, 251, 252),
            Margin = new Padding(0),
            Padding = new Padding(8, 6, 8, 6),
            Height = 44,
            MinimumSize = new Size(0, 44)
        };

        for (var i = 0; i < actionButtonCount; i++)
        {
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        }

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        return panel;
    }

    private static void ConfigurePathLabel(Label label)
    {
        label.AutoSize = false;
        label.Dock = DockStyle.Fill;
        label.Height = 36;
        label.Font = new Font(
            label.Font.FontFamily,
            Math.Max(6F, label.Font.Size - 1F),
            label.Font.Style,
            GraphicsUnit.Point);
        label.TextAlign = ContentAlignment.MiddleRight;
        label.AutoEllipsis = false;
        label.Margin = new Padding(10, 2, 0, 0);
    }

    private static void ConfigureActionButtonWithFixedWidth(Button button, string text, Color backColor, int minWidth)
    {
        ConfigureActionButton(button, text, backColor);
        var minButtonWidth = TextRenderer.MeasureText(text, button.Font).Width + 24;
        button.AutoSize = false;
        button.Width = Math.Max(minWidth, minButtonWidth);
        button.Height = 32;
        button.Padding = new Padding(8, 0, 8, 0);
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.Margin = new Padding(0);
    }

    private sealed class SingleLinePathLabel : Label
    {
        private const string TrailingReserveSample = "WW";

        protected override void OnPaint(PaintEventArgs e)
        {
            var trailingReservePixels = TextRenderer.MeasureText(
                TrailingReserveSample,
                Font,
                Size.Empty,
                TextFormatFlags.NoPadding).Width;
            var availableWidth = Math.Max(1, ClientRectangle.Width - trailingReservePixels);
            var renderText = DailyTrackingForm.TruncatePathFromStart(Text, Font, availableWidth);
            var drawBounds = new Rectangle(ClientRectangle.X, ClientRectangle.Y, availableWidth, ClientRectangle.Height);

            var flags = TextFormatFlags.SingleLine |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix |
                TextFormatFlags.PreserveGraphicsClipping;

            flags |= TextAlign switch
            {
                ContentAlignment.TopLeft or ContentAlignment.MiddleLeft or ContentAlignment.BottomLeft => TextFormatFlags.Left,
                ContentAlignment.TopCenter or ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter => TextFormatFlags.HorizontalCenter,
                _ => TextFormatFlags.Right
            };

            TextRenderer.DrawText(e.Graphics, renderText, Font, drawBounds, ForeColor, flags);
        }
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

        if (!TryShowFolderNameSuggestion("Nombre sugerido de carpeta", "Indica un prefijo opcional. Copia la propuesta para pegarla al crear una carpeta nueva.", extension: null, out _))
        {
            return false;
        }

        var selectedPath = ShowInputReceivedFolderDialog();

        if (selectedPath is null)
        {
            return false;
        }

        if (_currentProject is not null)
        {
            _currentProject.LastEntryFolderPath = _sharedPathService.ToStoredPath(selectedPath, directory: true);
        }

        UpdateInputReceivedPathDisplay(selectedPath);
        PersistCurrentEntry();
        return true;
    }

    private bool TryShowFolderNameSuggestion(string title, string helpText, string? extension, out string proposedName)
    {
        proposedName = string.Empty;

        if (_currentEntry is null || _currentProject is null)
        {
            return false;
        }

        var area = GetFieldValue(_currentEntry, "area") ?? string.Empty;
        var package = GetFieldValue(_currentEntry, "package") ?? string.Empty;

        using var suggestionDialog = new FolderNameSuggestionDialog(
            title,
            helpText,
            _currentProject.FolderNamePrefix ?? string.Empty,
            _currentEntry.EntryNumber,
            area,
            package,
            extension);

        if (suggestionDialog.ShowDialog(this) != DialogResult.OK)
        {
            return false;
        }

        if (!string.Equals(_currentProject.FolderNamePrefix ?? string.Empty, suggestionDialog.Prefix, StringComparison.Ordinal))
        {
            _currentProject.FolderNamePrefix = suggestionDialog.Prefix;
            _hasUnsavedChanges = true;
            UpdateAutosaveLabel();
        }

        proposedName = suggestionDialog.ProposedName;
        return true;
    }

    private string? ShowInputReceivedFolderDialog()
    {
        var currentPath = _inputReceivedPathLabel.Tag as string;
        var initialPath = ResolveInputReceivedDialogInitialPath(currentPath);

        using var dialog = new FolderBrowserDialog
        {
            Description = "Selecciona la carpeta de documentación",
            UseDescriptionForTitle = true,
            SelectedPath = initialPath ?? string.Empty
        };

        return dialog.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath)
            ? dialog.SelectedPath
            : null;
    }

    private string? ResolveInputReceivedDialogInitialPath(string? currentPath)
    {
        if (_currentProject is null)
        {
            return null;
        }

        var lastFolder = _sharedPathService.Resolve(_currentProject.LastEntryFolderPath, directory: true);
        if (!string.IsNullOrWhiteSpace(lastFolder) && Directory.Exists(lastFolder))
        {
            return lastFolder;
        }

        foreach (var entry in _currentProject.Entries.Where(item => item.EntryId != _currentEntry?.EntryId).OrderByDescending(item => item.EntryNumber))
        {
            if (entry.Values.TryGetValue(InputReceivedPathKey, out var entryPath) &&
                _sharedPathService.Resolve(entryPath, directory: true) is { } localPath && Directory.Exists(localPath))
            {
                return localPath;
            }
        }

        return !string.IsNullOrWhiteSpace(currentPath) && Directory.Exists(currentPath)
            ? currentPath
            : null;
    }

    private void InputReceivedPathLabel_Click(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || _currentEntry is null)
        {
            return;
        }

        var path = ResolvePathWithRecovery(GetFieldValue(_currentEntry, InputReceivedPathKey), directory: true);
        if (path is null) return;
        UpdateInputReceivedPathDisplay(path);
        TryOpenPath(path);
    }

    private void InputReceivedPathLabel_MouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right)
        {
            return;
        }

        ShowClearPathMenu(_inputReceivedPathLabel, e.Location, "Quitar ruta de carpeta de documentación", () =>
        {
            UpdateInputReceivedPathDisplay(null);
            PersistCurrentEntry();
        });
    }

    private void InputReceivedPathLabel_Resize(object? sender, EventArgs e)
    {
        if (_inputReceivedPathLabel.Tag is string currentPath)
        {
            UpdateInputReceivedPathDisplay(currentPath);
            return;
        }

        UpdateInputReceivedPathDisplay(null);
    }

    private void CreateMarkdownButton_Click(object? sender, EventArgs e)
    {
        TryCreateOrUpdateMarkdownFile(useSelectedPathWithoutDialog: false);
    }

    private void UpdateMarkdownButton_Click(object? sender, EventArgs e)
    {
        TryCreateOrUpdateMarkdownFile(useSelectedPathWithoutDialog: true);
    }

    private bool TryCreateOrUpdateMarkdownFile(bool useSelectedPathWithoutDialog)
    {
        if (_currentEntry is null)
        {
            return false;
        }

        string? selectedFilePath;

        if (useSelectedPathWithoutDialog)
        {
            var storedPath = GetFieldValue(_currentEntry, MarkdownFilePathKey);
            if (string.IsNullOrWhiteSpace(storedPath))
            {
                MessageBox.Show(this, "No hay archivo Markdown seleccionado para actualizar. Usa 'Crear MD' para elegirlo primero.",
                    "Actualizar MD", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            selectedFilePath = ResolvePathWithRecovery(storedPath, directory: false);
            if (selectedFilePath is null) return false;
        }
        else
        {
            if (!TryShowFolderNameSuggestion("Nombre sugerido de archivo Markdown", "Indica un prefijo opcional. Se usara como nombre inicial al crear el archivo.", extension: ".md", out var proposedFileName))
            {
                return false;
            }

            selectedFilePath = ShowMarkdownSaveFileDialog(proposedFileName);
        }

        if (string.IsNullOrWhiteSpace(selectedFilePath))
        {
            if (useSelectedPathWithoutDialog)
            {
                MessageBox.Show(
                    this,
                    "No hay archivo Markdown seleccionado para actualizar. Usa 'Crear MD' para elegirlo primero.",
                    "Actualizar MD",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            return false;
        }

        selectedFilePath = _sharedPathService.Resolve(selectedFilePath);
        if (string.IsNullOrWhiteSpace(selectedFilePath))
        {
            MessageBox.Show(this, "La ruta del archivo MD no es válida en este equipo.", "Ruta de archivo MD", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (useSelectedPathWithoutDialog && !File.Exists(selectedFilePath))
        {
            MessageBox.Show(
                this,
                "El archivo Markdown seleccionado no existe. Usa 'Crear MD' para seleccionarlo o crearlo.",
                "Actualizar MD",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        var templatePath = ResolveMarkdownTemplatePath();
        var targetFileExists = File.Exists(selectedFilePath);

        if (!targetFileExists && templatePath is null)
        {
            MessageBox.Show(
                this,
                "No se encontro la plantilla Tareas - copia.md. Verifica que el archivo exista en la carpeta del ejecutable o del proyecto.",
                useSelectedPathWithoutDialog ? "Actualizar MD" : "Crear MD",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        if (!_markdownExporter.TryCreateOrUpdate(selectedFilePath, templatePath ?? string.Empty, _currentEntry, out var synchronizedNotesText, out var errorMessage))
        {
            MessageBox.Show(
                this,
                $"No se pudo crear/modificar el archivo Markdown.\n\n{errorMessage}",
                useSelectedPathWithoutDialog ? "Actualizar MD" : "Crear MD",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return false;
        }

        var selectedDirectory = Path.GetDirectoryName(selectedFilePath);

        if (!useSelectedPathWithoutDialog && _currentProject is not null && !string.IsNullOrWhiteSpace(selectedDirectory))
        {
            _currentProject.LastMarkdownParentPath = _sharedPathService.ToStoredPath(selectedDirectory, directory: true);
        }

        ApplySynchronizedNotesToCurrentEntry(synchronizedNotesText);
        UpdateMarkdownFilePathDisplay(selectedFilePath);
        PersistCurrentEntry();
        return true;
    }

    private string? ShowMarkdownSaveFileDialog(string fileName)
    {
        var currentPath = _markdownFilePathLabel.Tag as string;
        var initialDirectory = ResolveMarkdownDialogInitialDirectory(currentPath);

        using var dialog = new SaveFileDialog
        {
            Title = "Crear/modificar archivo Markdown",
            Filter = "Markdown (*.md)|*.md|Todos los archivos (*.*)|*.*",
            DefaultExt = "md",
            AddExtension = true,
            OverwritePrompt = false,
            InitialDirectory = initialDirectory ?? string.Empty,
            FileName = fileName
        };

        return dialog.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.FileName)
            ? dialog.FileName
            : null;
    }

    private string? ResolveMarkdownDialogInitialDirectory(string? currentPath)
    {
        var lastMarkdownDirectory = _sharedPathService.Resolve(_currentProject?.LastMarkdownParentPath, directory: true);
        if (!string.IsNullOrWhiteSpace(lastMarkdownDirectory) && Directory.Exists(lastMarkdownDirectory))
        {
            return lastMarkdownDirectory;
        }

        if (_currentProject is not null)
        {
            foreach (var entry in _currentProject.Entries.Where(item => item.EntryId != _currentEntry?.EntryId).OrderByDescending(item => item.EntryNumber))
            {
                if (entry.Values.TryGetValue(MarkdownFilePathKey, out var entryPath) && !string.IsNullOrWhiteSpace(entryPath))
                {
                    var entryDirectory = Path.GetDirectoryName(_sharedPathService.Resolve(entryPath));
                    if (!string.IsNullOrWhiteSpace(entryDirectory) && Directory.Exists(entryDirectory))
                    {
                        return entryDirectory;
                    }
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            var currentDirectory = Path.GetDirectoryName(currentPath);

            if (!string.IsNullOrWhiteSpace(currentDirectory) && Directory.Exists(currentDirectory))
            {
                return currentDirectory;
            }
        }

        var inputReceivedPath = _inputReceivedPathLabel.Tag as string;

        if (!string.IsNullOrWhiteSpace(inputReceivedPath) && Directory.Exists(inputReceivedPath))
        {
            return inputReceivedPath;
        }

        var projectsFolderPath = _workspaceService.GetProjectsFolderPath();
        return Directory.Exists(projectsFolderPath) ? projectsFolderPath : null;
    }

    private static string? ResolveMarkdownTemplatePath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Tareas - copia.md"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Tareas - copia.md"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Tareas - copia.md"),
            Path.Combine(Directory.GetCurrentDirectory(), "Tareas - copia.md")
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);

            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }

    private void MarkdownFilePathLabel_Click(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || _currentEntry is null)
        {
            return;
        }

        var filePath = ResolvePathWithRecovery(GetFieldValue(_currentEntry, MarkdownFilePathKey), directory: false);
        if (filePath is null) return;
        UpdateMarkdownFilePathDisplay(filePath);
        TryOpenPath(filePath);
    }

    private void TryOpenPath(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            MessageBox.Show(this, $"No se pudo abrir la ruta.\n\n{ex.Message}", "Abrir ruta", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void MarkdownFilePathLabel_MouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right)
        {
            return;
        }

        ShowClearPathMenu(_markdownFilePathLabel, e.Location, "Quitar ruta de archivo MD", () =>
        {
            UpdateMarkdownFilePathDisplay(null);
            PersistCurrentEntry();
        });
    }

    private void ShowClearPathMenu(Control sourceControl, Point location, string menuText, Action clearAction)
    {
        if (_currentEntry is null || sourceControl.Tag is not string { Length: > 0 })
        {
            return;
        }

        var menu = new ContextMenuStrip();
        var clearItem = new ToolStripMenuItem(menuText);
        clearItem.Click += (_, _) => clearAction();
        menu.Items.Add(clearItem);
        menu.Closed += (_, _) =>
        {
            if (menu.IsDisposed)
            {
                return;
            }

            if (IsHandleCreated && !IsDisposed)
            {
                BeginInvoke(new Action(() =>
                {
                    if (!menu.IsDisposed)
                    {
                        menu.Dispose();
                    }
                }));
                return;
            }

            menu.Dispose();
        };
        menu.Show(sourceControl, location);
    }

    private void MarkdownFilePathLabel_Resize(object? sender, EventArgs e)
    {
        if (_markdownFilePathLabel.Tag is string currentPath)
        {
            UpdateMarkdownFilePathDisplay(currentPath);
            return;
        }

        UpdateMarkdownFilePathDisplay(null);
    }

    private static string TruncatePathFromStart(string path, Font font, int maxWidth)
    {
        if (string.IsNullOrWhiteSpace(path) || maxWidth <= 0)
        {
            return path;
        }

        if (TextRenderer.MeasureText(path, font).Width <= maxWidth)
        {
            return path;
        }

        const string ellipsis = "...";

        if (TextRenderer.MeasureText(ellipsis, font).Width >= maxWidth)
        {
            return ellipsis;
        }

        var low = 0;
        var high = path.Length - 1;
        var bestStart = path.Length - 1;

        while (low <= high)
        {
            var mid = (low + high) / 2;
            var candidate = ellipsis + path[mid..];

            if (TextRenderer.MeasureText(candidate, font).Width <= maxWidth)
            {
                bestStart = mid;
                high = mid - 1;
            }
            else
            {
                low = mid + 1;
            }
        }

        return ellipsis + path[bestStart..];
    }

    private void UpdateInputReceivedPathDisplay(string? path)
    {
        var selectedPath = string.IsNullOrWhiteSpace(path) ? null : path.Trim();
        var pathExists = selectedPath is not null && Directory.Exists(selectedPath);

        _inputReceivedPathLabel.Tag = selectedPath;
        _inputReceivedPathLabel.Text = selectedPath is null
            ? "Sin ruta de carpeta de documentación"
            : selectedPath;
        _inputReceivedPathLabel.ForeColor = selectedPath is null ? Color.FromArgb(132, 140, 152) : Color.FromArgb(22, 58, 92);
        _inputReceivedPathLabel.Cursor = selectedPath is null ? Cursors.Default : Cursors.Hand;

        _toolTip.SetToolTip(
            _inputReceivedPathLabel,
            selectedPath is null
                ? "Seleccion opcional: carpeta de documentación. Clic derecho para limpiar."
                : pathExists
                    ? $"{selectedPath}\n(Clic para abrir en el explorador de archivos, clic derecho para limpiar)"
                    : $"{selectedPath}\n(Clic para localizar la carpeta, clic derecho para limpiar)");
    }

    private void UpdateMarkdownFilePathDisplay(string? path)
    {
        var selectedPath = string.IsNullOrWhiteSpace(path) ? null : path.Trim();
        var fileExists = selectedPath is not null && File.Exists(selectedPath);

        _markdownFilePathLabel.Tag = selectedPath;
        _markdownFilePathLabel.Text = selectedPath is null
            ? "Sin archivo MD"
            : selectedPath;
        _markdownFilePathLabel.ForeColor = selectedPath is null ? Color.FromArgb(132, 140, 152) : Color.FromArgb(22, 58, 92);
        _markdownFilePathLabel.Cursor = selectedPath is null ? Cursors.Default : Cursors.Hand;

        _toolTip.SetToolTip(
            _markdownFilePathLabel,
            selectedPath is null
                ? "Seleccion opcional: archivo Markdown de esta ficha. Clic derecho para limpiar."
                : fileExists
                    ? $"{selectedPath}\n(Clic para abrir el archivo, clic derecho para limpiar)"
                    : $"{selectedPath}\n(Clic para localizar el archivo, clic derecho para limpiar)");
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

    private static string FormatBoldNoteSegment(string text)
    {
        return $"**{text}**";
    }

    private string BuildCommentPrefixWithCurrentStatus()
    {
        var prefix = BuildCommentPrefix();
        var status = _currentEntry is null ? null : GetFieldValue(_currentEntry, StatusFieldKey);

        return string.IsNullOrWhiteSpace(status)
            ? prefix
            : prefix + FormatBoldNoteSegment(status.Trim()) + " ";
    }

    private void CommentsTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not RichTextBox textBox || e.KeyCode != Keys.Enter)
        {
            return;
        }

        textBox.SelectedText = Environment.NewLine + BuildCommentPrefixWithCurrentStatus();
        e.SuppressKeyPress = true;
    }

    private void CommentsTextBox_KeyPress(object? sender, KeyPressEventArgs e)
    {
        if (sender is not RichTextBox textBox || char.IsControl(e.KeyChar))
        {
            return;
        }

        if (ShouldInsertCommentPrefixAtCaret(textBox))
        {
            textBox.SelectedText = BuildCommentPrefixWithCurrentStatus() + e.KeyChar;
            e.Handled = true;
        }
    }

    private static bool ShouldInsertCommentPrefixAtCaret(RichTextBox textBox)
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

    private void ApplyNotesFormatting(RichTextBox notesTextBox)
    {
        var selectionStart = notesTextBox.SelectionStart;
        var selectionLength = notesTextBox.SelectionLength;

        notesTextBox.SelectAll();
        notesTextBox.SelectionColor = notesTextBox.ForeColor;

        foreach (Match match in NoteTimestampRegex.Matches(notesTextBox.Text))
        {
            notesTextBox.Select(match.Index, match.Length);
            notesTextBox.SelectionColor = Color.Red;
        }

        if (_statusFieldDefinition is not null)
        {
            foreach (Match match in NoteStatusRegex.Matches(notesTextBox.Text))
            {
                var status = match.Groups["status"].Value.Trim();
                var knownStatus = _statusFieldDefinition.Options.FirstOrDefault(option =>
                    string.Equals(option, status, StringComparison.OrdinalIgnoreCase));

                if (knownStatus is null)
                {
                    continue;
                }

                notesTextBox.Select(match.Index, match.Length);
                notesTextBox.SelectionColor = OptionColorHelper.GetColor(_statusFieldDefinition, knownStatus);
            }
        }

        notesTextBox.Select(
            Math.Min(selectionStart, notesTextBox.TextLength),
            Math.Min(selectionLength, Math.Max(0, notesTextBox.TextLength - selectionStart)));

        if (selectionLength == 0)
        {
            notesTextBox.SelectionColor = notesTextBox.ForeColor;
        }
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
            _hasUnsavedChanges = true;
            UpdateAutosaveLabel();
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
        _hasUnsavedChanges = true;
        UpdateAutosaveLabel();
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

    private static Func<string, Color>? BuildOptionColorResolver(TrackingFieldDefinition? field)
    {
        return field is null ? null : value => OptionColorHelper.GetColor(field, value);
    }

    private void DailyTrackingForm_Load(object? sender, EventArgs e)
    {
        FitWindowToContent();
        try
        {
            LoadInitialProject();
        }
        catch (DirectoryNotFoundException ex)
        {
            var storedFolder = _workspaceService.GetStoredProjectsFolderPath();
            var recovered = ResolvePathWithRecovery(storedFolder, directory: true);
            if (recovered is not null)
            {
                _workspaceService.SetProjectsFolderPath(recovered);
                UpdateProjectsFolderDisplay();
                try
                {
                    LoadInitialProject();
                }
                catch (Exception retryError) when (IsSharedProjectError(retryError))
                {
                    ShowSharedProjectError(retryError);
                }
                return;
            }
            MessageBox.Show(this, $"{ex.Message}\n\nSelecciona la carpeta de proyectos en este PC.",
                "Carpeta no disponible", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            ChooseProjectsFolderButton_Click(this, EventArgs.Empty);
        }
        catch (Exception ex) when (IsSharedProjectError(ex))
        {
            ShowSharedProjectError(ex);
        }
    }

    private void LoadInitialProject()
    {
        RefreshProjectSelector(Guid.Empty);
        var project = _workspaceService.TryOpenLastProject();
        if (project is null)
        {
            var firstProject = _workspaceService.GetProjects().FirstOrDefault();
            project = firstProject is null
                ? PromptCreateProject(closeWhenCancelled: false)
                : _workspaceService.OpenProject(firstProject.ProjectId);
        }
        if (project is not null)
        {
            LoadProject(project);
            HandleMergeAfterOpen();
        }
    }

    // Sizes the window tall enough to show every ficha field without vertical scrolling, capped to the screen's working area.
    private void FitWindowToContent()
    {
        // Chrome above/around the dynamic field grid: hero + toolbar + content/editor paddings + heading + scroll top padding.
        const int chromeHeight = 160 + 76 + 18 + 24 + 28 + 8;
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

    private void LoadProject(TrackingProject project, bool createInitialEntry = true)
    {
        _currentProject = project;
        _hasUnsavedChanges = false;
        RefreshEditableOptionItems(project);
        _assignedToFilterDropDown.SetOptions(project.Entries
            .Select(entry => GetFieldValue(entry, "assignedTo"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase));

        if (createInitialEntry && _currentProject.Entries.Count == 0)
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
        else
        {
            UpdateAutosaveLabel();
        }
    }

    private void LoadEntry(TrackingEntry entry)
    {
        if (_pendingLongTextChanges && !ReferenceEquals(_currentEntry, entry))
        {
            CommitPendingLongTextChanges();
        }

        _currentEntry = entry;
        EnsureUpdatedDateAutoFlag(entry);
        _suppressEvents = true;

        foreach (var field in _schema.Fields)
        {
            var value = entry.Values.TryGetValue(field.Key, out var storedValue) ? storedValue : null;
            var control = _fieldControls[field.Key];

            switch (control)
            {
                case RichTextBox richTextBox:
                    richTextBox.Text = value ?? string.Empty;
                    ApplyNotesFormatting(richTextBox);
                    break;

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
        var markdownFilePath = entry.Values.TryGetValue(MarkdownFilePathKey, out var storedMarkdownPath) ? storedMarkdownPath : null;
        UpdateInputReceivedPathDisplay(_sharedPathService.Resolve(inputReceivedPath, directory: true) ?? inputReceivedPath);
        UpdateMarkdownFilePathDisplay(_sharedPathService.Resolve(markdownFilePath) ?? markdownFilePath);
        SetUpdatedDateAutoCheckboxState(ReadUpdatedDateAutoFlag(entry));

        _suppressEvents = false;
        _pendingLongTextChanges = false;
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
        var markdownFilePath = _markdownFilePathLabel.Tag as string;
        _currentEntry.Values[InputReceivedPathKey] = string.IsNullOrWhiteSpace(inputReceivedPath) ? null : _sharedPathService.ToStoredPath(inputReceivedPath, directory: true);
        _currentEntry.Values[MarkdownFilePathKey] = string.IsNullOrWhiteSpace(markdownFilePath) ? null : _sharedPathService.ToStoredPath(markdownFilePath);

        _currentEntry.UpdatedAtLocal = DateTime.Now;
        _pendingLongTextChanges = false;
        _hasUnsavedChanges = true;
        RefreshEntriesList();
        UpdateProjectSummary();
        UpdateAutosaveLabel();
    }

    private void ApplySynchronizedNotesToCurrentEntry(string synchronizedNotesText)
    {
        if (_currentEntry is null)
        {
            return;
        }

        var normalizedNotes = NormalizeNotesForEditor(synchronizedNotesText);
        _currentEntry.Values[NotesFieldKey] = string.IsNullOrWhiteSpace(normalizedNotes) ? null : normalizedNotes;

        if (!_fieldControls.TryGetValue(NotesFieldKey, out var notesControl) || notesControl is not RichTextBox notesTextBox)
        {
            return;
        }

        var previousSuppressState = _suppressEvents;
        _suppressEvents = true;
        notesTextBox.Text = normalizedNotes;
        notesTextBox.SelectionStart = notesTextBox.TextLength;
        ApplyNotesFormatting(notesTextBox);
        _suppressEvents = previousSuppressState;
    }

    private static string NormalizeNotesForEditor(string? notesText)
    {
        var normalized = (notesText ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Trim('\n', '\r');

        return normalized.Replace("\n", Environment.NewLine, StringComparison.Ordinal);
    }

    private void EnsureInitialStatusNote(TrackingEntry entry)
    {
        var currentNotes = GetFieldValue(entry, NotesFieldKey);

        if (!string.IsNullOrWhiteSpace(currentNotes))
        {
            return;
        }

        AppendNoteEntry(entry, "Nuevo");
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

        var line = BuildCommentPrefix() + FormatBoldNoteSegment(normalizedNoteText);
        var combinedNotes = string.IsNullOrWhiteSpace(currentNotes)
            ? line
            : currentNotes + Environment.NewLine + line;

        entry.Values[NotesFieldKey] = combinedNotes;

        if (!_fieldControls.TryGetValue(NotesFieldKey, out var notesControl) || notesControl is not RichTextBox notesTextBox)
        {
            return;
        }

        var previousSuppressState = _suppressEvents;
        _suppressEvents = true;
        notesTextBox.Text = combinedNotes;
        notesTextBox.SelectionStart = notesTextBox.TextLength;
        ApplyNotesFormatting(notesTextBox);
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

        var lastText = StripBoldMarkers(StripCommentPrefix(lines[^1]));
        return string.Equals(lastText, nextNoteText, StringComparison.OrdinalIgnoreCase);
    }

    private static string StripBoldMarkers(string text)
    {
        return text.StartsWith("**", StringComparison.Ordinal) && text.EndsWith("**", StringComparison.Ordinal) && text.Length >= 4
            ? text[2..^2]
            : text;
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

    private bool SaveCurrentProject()
    {
        CommitPendingLongTextChanges();

        if (_currentProject is null)
        {
            return true;
        }

        var selectedEntryId = _currentEntry?.EntryId;
        try
        {
            var merged = _workspaceService.SaveProject(_currentProject);
            var incoming = merged.IncorporatedChanges;
            merged = ResolveConflicts(merged);
            LoadMergedProject(merged, selectedEntryId);
            ShowMergeNotice(incoming);
            ShowOtherEditorsNotice();
            return true;
        }
        catch (Exception ex) when (IsSharedProjectError(ex))
        {
            ShowSharedProjectError(ex);
            return false;
        }
    }

    private ProjectMergeResult ResolveConflicts(ProjectMergeResult merged)
    {
        while (merged.Conflicts.Count > 0)
        {
            var conflict = merged.Conflicts[0];
            using var dialog = new FieldConflictDialog(conflict);
            if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedChangeId is not { } selected)
            {
                break;
            }
            merged = _workspaceService.ResolveConflict(conflict, selected);
        }
        return merged;
    }

    private void LoadMergedProject(ProjectMergeResult merged, Guid? selectedEntryId)
    {
        _currentEntry = null;
        _pendingConflictCount = merged.Conflicts.Count;
        LoadProject(merged.Project, createInitialEntry: false);
        if (selectedEntryId is { } id && merged.Project.Entries.FirstOrDefault(entry => entry.EntryId == id) is { } selected)
        {
            LoadEntry(selected);
        }
        _pendingConflictCount = merged.Conflicts.Count;
        UpdateAutosaveLabel();
    }

    private void HandleMergeAfterOpen(Guid? selectedEntryId = null)
    {
        ShowOtherEditorsNotice();
        if (_hasUnsavedChanges) return;
        var merged = _workspaceService.LastMergeResult;
        if (merged is null || merged.Project.ProjectId != _currentProject?.ProjectId) return;
        var incoming = merged.IncorporatedChanges;
        merged = ResolveConflicts(merged);
        LoadMergedProject(merged, selectedEntryId);
        ShowMergeNotice(incoming);
    }

    private void ShowMergeNotice(int count)
    {
        if (count > 0)
        {
            MessageBox.Show(this, $"Se han incorporado {count} cambios sincronizados de este proyecto.",
                "JSON fusionados", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void ShowOtherEditorsNotice()
    {
        var newlySeen = _workspaceService.OtherEditors
            .Where(editor => _announcedEditorSessions.Add(editor.SessionId))
            .ToList();
        if (newlySeen.Count == 0) return;
        var people = string.Join(Environment.NewLine, newlySeen.Select(editor =>
            $"• {editor.UserName} ({editor.MachineName}), activo a las {editor.UpdatedAtUtc.ToLocalTime():HH:mm}"));
        MessageBox.Show(this,
            $"Según los archivos ya sincronizados, también tienen abierto este proyecto:{Environment.NewLine}{Environment.NewLine}{people}{Environment.NewLine}{Environment.NewLine}Los cambios se fusionarán al guardar o volver a abrir el proyecto.",
            "Otros usuarios en el proyecto", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // If there are unsaved changes, prompts to save/discard/cancel. Returns true when the caller should proceed
    // (switch project, change folder or close the app); false when the user cancelled the action.
    private bool ConfirmUnsavedChangesBeforeProceeding()
    {
        CommitPendingLongTextChanges();

        if (_hasUnsavedChanges)
        {
            var result = MessageBox.Show(
                this,
                "Hay cambios sin guardar en el proyecto actual. ¿Deseas guardarlos antes de continuar?",
                "Cambios sin guardar",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning);

            switch (result)
            {
                case DialogResult.Yes:
                    if (!SaveCurrentProject()) return false;
                    break;

                case DialogResult.No:
                    _hasUnsavedChanges = false;
                    return true;

                default:
                    return false;
            }
        }

        try
        {
            if (!PromptUpdateMarkdownFilesForProject()) return false;
        }
        catch (Exception ex) when (IsSharedProjectError(ex))
        {
            ShowSharedProjectError(ex);
            return false;
        }
        return true;
    }

    // Offers to regenerate the .md file of every task in the current project that has one assigned via "Crear MD".
    // Tasks without a Markdown file selected are skipped silently.
    private bool PromptUpdateMarkdownFilesForProject()
    {
        var entriesWithMarkdown = GetEntriesWithMarkdownFile();

        if (entriesWithMarkdown.Count == 0)
        {
            return true;
        }

        var question = entriesWithMarkdown.Count == 1
            ? "Hay 1 tarea con un archivo Markdown asociado. ¿Deseas actualizar ese archivo .md ahora?"
            : $"Hay {entriesWithMarkdown.Count} tareas con archivo Markdown asociado. ¿Deseas actualizar esos archivos .md ahora?";

        var result = MessageBox.Show(
            this,
            question,
            "Actualizar archivos Markdown",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
        {
            return UpdateMarkdownFilesForEntries(entriesWithMarkdown);
        }
        return true;
    }

    private List<TrackingEntry> GetEntriesWithMarkdownFile()
    {
        if (_currentProject is null)
        {
            return [];
        }

        return _currentProject.Entries
            .Where(entry => !string.IsNullOrWhiteSpace(GetFieldValue(entry, MarkdownFilePathKey)))
            .ToList();
    }

    private bool UpdateMarkdownFilesForEntries(IReadOnlyList<TrackingEntry> entries)
    {
        var templatePath = ResolveMarkdownTemplatePath();
        var errors = new List<string>();

        foreach (var entry in entries)
        {
            var markdownFilePath = ResolvePathWithRecovery(GetFieldValue(entry, MarkdownFilePathKey), directory: false);

            if (string.IsNullOrWhiteSpace(markdownFilePath) || !File.Exists(markdownFilePath))
            {
                errors.Add($"Tarea #{entry.EntryNumber}: archivo MD no disponible en este equipo.");
                continue;
            }

            if (!_markdownExporter.TryCreateOrUpdate(markdownFilePath, templatePath ?? string.Empty, entry, out var synchronizedNotesText, out var errorMessage))
            {
                errors.Add($"Tarea #{entry.EntryNumber}: {errorMessage}");
                continue;
            }

            entry.Values[NotesFieldKey] = string.IsNullOrWhiteSpace(synchronizedNotesText) ? null : synchronizedNotesText;

            if (_currentEntry is not null && _currentEntry.EntryId == entry.EntryId)
            {
                ApplySynchronizedNotesToCurrentEntry(synchronizedNotesText);
            }
        }

        if (_currentProject is not null && !SaveCurrentProject())
        {
            return false;
        }

        if (errors.Count > 0)
        {
            MessageBox.Show(
                this,
                "No se pudieron actualizar algunos archivos Markdown:" + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, errors),
                "Actualizar archivos Markdown",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        return true;
    }

    private static string? ReadControlValue(Control control)
    {
        return control switch
        {
            RichTextBox richTextBox => string.IsNullOrWhiteSpace(richTextBox.Text) ? null : richTextBox.Text.Trim(),
            TextBox textBox => string.IsNullOrWhiteSpace(textBox.Text) ? null : textBox.Text.Trim(),
            ComboBox comboBox => string.IsNullOrWhiteSpace(comboBox.Text) ? null : comboBox.Text,
            DateTimePicker datePicker => datePicker.Checked ? datePicker.Value.ToString("yyyy-MM-dd") : null,
            _ => null
        };
    }

    private void RefreshProjectSelector(Guid selectedProjectId)
    {
        var projects = _workspaceService.GetProjects();
        var previousSuppressEvents = _suppressEvents;
        _suppressEvents = true;
        try
        {
            _projectSelector.Items.Clear();
            foreach (var project in projects)
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
        }
        finally
        {
            _suppressEvents = previousSuppressEvents;
        }
    }

    private void RestoreSelectedProject()
    {
        if (_currentProject is null)
        {
            return;
        }

        var previousSuppressEvents = _suppressEvents;
        _suppressEvents = true;
        try
        {
            for (var index = 0; index < _projectSelector.Items.Count; index++)
            {
                if (_projectSelector.Items[index] is ProjectSelectorItem item && item.ProjectId == _currentProject.ProjectId)
                {
                    _projectSelector.SelectedIndex = index;
                    return;
                }
            }
        }
        finally
        {
            _suppressEvents = previousSuppressEvents;
        }
    }

    private void RefreshEntriesList()
    {
        if (_currentProject is not null)
        {
            _assignedToFilterDropDown.UpdateOptions(_currentProject.Entries
                .Select(entry => GetFieldValue(entry, "assignedTo"))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase));
        }

        _entriesListView.BeginUpdate();
        _entriesListView.Items.Clear();

        foreach (var entry in GetSortedEntries())
        {
            var listItem = new ListViewItem(GetFieldValue(entry, StatusFieldKey) ?? string.Empty)
            {
                Tag = entry.EntryId,
                UseItemStyleForSubItems = false
            };
            listItem.SubItems.Add(GetFieldValue(entry, PriorityFieldKey) ?? string.Empty);
            listItem.SubItems.Add(entry.EntryNumber.ToString());
            listItem.SubItems.Add(GetEntryDate(entry));
            listItem.SubItems.Add(GetFieldValue(entry, "discipline") ?? "-");
            listItem.SubItems.Add(GetFieldValue(entry, "area") ?? "-");
            listItem.SubItems.Add(GetFieldValue(entry, "package") ?? "-");
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

        var statusFilterValues = _statusFilterDropDown.GetSelectedValues();

        if (statusFilterValues.Count > 0)
        {
            entries = entries.Where(entry => statusFilterValues.Contains(GetFieldValue(entry, "status") ?? string.Empty, StringComparer.OrdinalIgnoreCase));
        }

        var priorityFilterValues = _priorityFilterDropDown.GetSelectedValues();

        if (priorityFilterValues.Count > 0)
        {
            entries = entries.Where(entry => priorityFilterValues.Contains(GetFieldValue(entry, "priority") ?? string.Empty, StringComparer.OrdinalIgnoreCase));
        }

        var assignedToFilterValues = _assignedToFilterDropDown.GetSelectedValues();
        if (assignedToFilterValues.Count > 0)
        {
            entries = entries.Where(entry => assignedToFilterValues.Contains(GetFieldValue(entry, "assignedTo") ?? string.Empty, StringComparer.OrdinalIgnoreCase));
        }

        var descending = _sortOrder == SortOrder.Descending;

        return _sortColumnIndex switch
        {
            0 => SortEntriesByRank(entries, StatusFieldKey, StatusRanks, descending),
            1 => SortEntriesByRank(entries, PriorityFieldKey, PriorityRanks, descending),
            2 => descending ? entries.OrderByDescending(entry => entry.EntryNumber) : entries.OrderBy(entry => entry.EntryNumber),
            3 => descending ? entries.OrderByDescending(GetSortableDate) : entries.OrderBy(GetSortableDate),
            4 => descending ? entries.OrderByDescending(entry => GetFieldValue(entry, "discipline") ?? string.Empty) : entries.OrderBy(entry => GetFieldValue(entry, "discipline") ?? string.Empty),
            5 => descending ? entries.OrderByDescending(entry => GetFieldValue(entry, "area") ?? string.Empty) : entries.OrderBy(entry => GetFieldValue(entry, "area") ?? string.Empty),
            6 => descending ? entries.OrderByDescending(entry => GetFieldValue(entry, "package") ?? string.Empty) : entries.OrderBy(entry => GetFieldValue(entry, "package") ?? string.Empty),
            _ => entries.OrderByDescending(entry => entry.UpdatedAtLocal)
        };
    }

    private static IEnumerable<TrackingEntry> SortEntriesByRank(
        IEnumerable<TrackingEntry> entries,
        string fieldKey,
        IReadOnlyDictionary<string, int> ranks,
        bool descending)
    {
        var rankedEntries = entries.Select(entry =>
        {
            var value = GetFieldValue(entry, fieldKey) ?? string.Empty;
            return (Entry: entry, Rank: ranks.TryGetValue(value, out var rank) ? rank : (int?)null);
        });

        return descending
            ? rankedEntries
                .OrderBy(item => item.Rank is null)
                .ThenByDescending(item => item.Rank)
                .Select(item => item.Entry)
            : rankedEntries
                .OrderBy(item => item.Rank is null)
                .ThenBy(item => item.Rank)
                .Select(item => item.Entry);
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

    // The path uses up to four lines in the space left by the project controls.
    private void UpdateProjectsFolderDisplay()
    {
        string? path;
        try
        {
            path = _workspaceService.GetProjectsFolderPath();
        }
        catch (DirectoryNotFoundException)
        {
            path = null;
        }
        var leftWidth = _projectSelectorRow.Padding.Horizontal + _projectSelectorRow.Controls.Cast<Control>()
            .Sum(control => (control.AutoSize ? control.GetPreferredSize(Size.Empty).Width : control.Width) + control.Margin.Horizontal);
        var maxWidth = Math.Max(220, ClientSize.Width - 48 - leftWidth - 18);
        var displayedPath = path is null
            ? "Selecciona la carpeta de proyectos"
            : FormatPathForLines(path, maxWidth - 16, _projectsFolderPathLabel.Font);
        _toolTip.SetToolTip(_chooseProjectsFolderButton, path);
        _projectsFolderPathLabel.Text = displayedPath;
        _toolTip.SetToolTip(_projectsFolderPathLabel, path is null
            ? "Selecciona la carpeta de proyectos"
            : $"{path}\n(Clic para abrir en el explorador de archivos)");

        var folderButtonSize = _chooseProjectsFolderButton.GetPreferredSize(Size.Empty);
        var displayedLines = displayedPath.Split(Environment.NewLine);
        var pathTextWidth = displayedLines.Max(line => TextRenderer.MeasureText(line, _projectsFolderPathLabel.Font,
            Size.Empty, TextFormatFlags.NoPadding).Width);
        var desiredWidth = Math.Max(folderButtonSize.Width, pathTextWidth) + 16;

        _folderButtonPanel.Width = Math.Min(desiredWidth, maxWidth);
        _chooseProjectsFolderButton.Left = _folderButtonPanel.Width - folderButtonSize.Width - 8;
        _projectsFolderPathLabel.Left = 0;
        _projectsFolderPathLabel.Width = _folderButtonPanel.Width;
    }

    private static string FormatPathForLines(string path, int maxLineWidth, Font font)
    {
        bool Fits(string text) => TextRenderer.MeasureText(text, font, Size.Empty,
            TextFormatFlags.NoPadding).Width <= maxLineWidth;
        var fullWidth = TextRenderer.MeasureText(path, font, Size.Empty, TextFormatFlags.NoPadding).Width;
        var lineCount = path.Length > 105 || fullWidth > maxLineWidth * 3
            ? 4 : path.Length > 55 || fullWidth > maxLineWidth ? 3 : 1;
        if (lineCount == 1) return path;
        var lines = new List<string>(lineCount);
        var start = 0;
        for (var lineIndex = 0; lineIndex < lineCount - 1; lineIndex++)
        {
            var target = start + (int)Math.Ceiling((double)(path.Length - start) / (lineCount - lineIndex));
            var maxCut = path.Length - (lineCount - lineIndex - 1);
            var cuts = Enumerable.Range(start + 1, maxCut - start)
                .Where(index => path[index - 1] is '\\' or '/')
                .Where(index => Fits(path[start..index]))
                .ToList();
            var cut = cuts.Count > 0
                ? cuts.MinBy(index => Math.Abs(index - target))
                : Math.Min(target, maxCut);
            while (cut > start + 1 && !Fits(path[start..cut])) cut--;
            lines.Add(path[start..cut]);
            start = cut;
        }
        var finalLine = path[start..];
        if (!Fits(finalLine))
        {
            var length = finalLine.Length;
            while (length > 0 && !Fits(finalLine[..length] + "…")) length--;
            finalLine = finalLine[..length] + "…";
        }
        lines.Add(finalLine);
        return string.Join(Environment.NewLine, lines);
    }

    private void UpdateAutosaveLabel()
    {
        _deleteEntryButton.Enabled = _currentEntry is not null;
        _newEntryButton.Enabled = _currentProject is not null;
        _saveEntryButton.Enabled = _currentProject is not null && _hasUnsavedChanges;
        _saveEntryButton.BackColor = _hasUnsavedChanges
            ? Color.FromArgb(232, 144, 34)
            : Color.FromArgb(96, 100, 112);
        _manageProjectUsersButton.Enabled = _currentProject is not null;
        _editorLayout.Enabled = _currentEntry is not null;

        if (_currentEntry is null)
        {
            UpdateInputReceivedPathDisplay(null);
            UpdateMarkdownFilePathDisplay(null);
            _autosaveLabel.Text = "Sin registro seleccionado";
            return;
        }

        _autosaveLabel.Text = _hasUnsavedChanges
            ? "Cambios sin guardar en este equipo"
            : _pendingConflictCount > 0
                ? $"Guardado localmente · {_pendingConflictCount} conflicto(s) pendiente(s)"
                : $"Guardado localmente: {_currentEntry.UpdatedAtLocal:dd/MM/yyyy HH:mm:ss}";
    }

    private void FieldValueChanged(object? sender, EventArgs e)
    {
        if (_suppressEvents)
        {
            return;
        }

        PersistCurrentEntry();
    }

    private void LongTextValueChanged(object? sender, EventArgs e)
    {
        if (_suppressEvents || _currentEntry is null || _pendingLongTextChanges)
        {
            return;
        }

        _pendingLongTextChanges = true;
        _hasUnsavedChanges = true;
        UpdateAutosaveLabel();
    }

    private void CommitPendingLongTextChanges()
    {
        if (_suppressEvents || !_pendingLongTextChanges)
        {
            return;
        }

        _pendingLongTextChanges = false;
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
            RestoreSelectedProject();
            return;
        }

        TrackingProject? project;
        try
        {
            project = _workspaceService.OpenProject(item.ProjectId);
        }
        catch (Exception ex) when (IsSharedProjectError(ex))
        {
            ShowSharedProjectError(ex);
            RestoreSelectedProject();
            return;
        }

        if (project is not null)
        {
            _currentEntry = null;
            LoadProject(project);
            HandleMergeAfterOpen();
        }
    }

    private void NewProjectButton_Click(object? sender, EventArgs e)
    {
        if (!ConfirmUnsavedChangesBeforeProceeding())
        {
            return;
        }

        TrackingProject? project;
        try
        {
            project = PromptCreateProject(closeWhenCancelled: false);
        }
        catch (Exception ex) when (IsSharedProjectError(ex))
        {
            ShowSharedProjectError(ex);
            return;
        }

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
        IReadOnlyList<TrackingProject> projects;
        try
        {
            projects = _workspaceService.GetProjects();
        }
        catch (Exception ex) when (IsSharedProjectError(ex))
        {
            ShowSharedProjectError(ex);
            return;
        }

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

        if (!ConfirmUnsavedChangesBeforeProceeding())
        {
            return;
        }

        var wasCurrentProject = projectToDelete.ProjectId == _currentProject?.ProjectId;
        try
        {
            _workspaceService.DeleteProject(projectToDelete);
        }
        catch (Exception ex) when (IsSharedProjectError(ex))
        {
            ShowSharedProjectError(ex);
            return;
        }

        if (!wasCurrentProject)
        {
            RefreshProjectSelector(_currentProject?.ProjectId ?? Guid.Empty);
            return;
        }

        _hasUnsavedChanges = false;
        _currentProject = null;
        _currentEntry = null;

        TrackingProject? nextProject;
        try
        {
            var nextAvailable = _workspaceService.GetProjects().FirstOrDefault();
            nextProject = nextAvailable is null
                ? PromptCreateProject(closeWhenCancelled: false)
                : _workspaceService.OpenProject(nextAvailable.ProjectId);
        }
        catch (Exception ex) when (IsSharedProjectError(ex))
        {
            ShowSharedProjectError(ex);
            nextProject = null;
        }

        if (nextProject is not null)
        {
            LoadProject(nextProject);
            HandleMergeAfterOpen();
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
        _hasUnsavedChanges = true;
        UpdateAutosaveLabel();
    }

    private void NewEntryButton_Click(object? sender, EventArgs e)
    {
        if (_currentProject is null)
        {
            return;
        }

        if (!ConfirmCurrentEntryChanges()) return;
        var assignedTo = _currentEntry is null ? null : GetFieldValue(_currentEntry, "assignedTo");
        var entry = _workspaceService.CreateEntry(_currentProject, _schema, assignedTo);
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

        CommitPendingLongTextChanges();
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
        _hasUnsavedChanges = true;
        SaveCurrentProject();
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
        var storedPath = _workspaceService.GetStoredProjectsFolderPath();
        var path = string.IsNullOrWhiteSpace(storedPath)
            ? _workspaceService.GetProjectsFolderPath()
            : ResolvePathWithRecovery(storedPath, directory: true);
        if (path is null) return;
        if (!string.IsNullOrWhiteSpace(storedPath))
            _workspaceService.SetProjectsFolderPath(path);
        UpdateProjectsFolderDisplay();
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    private void ChooseProjectsFolderButton_Click(object? sender, EventArgs e)
    {
        string currentFolderPath;
        try
        {
            currentFolderPath = _workspaceService.GetProjectsFolderPath();
        }
        catch (DirectoryNotFoundException)
        {
            currentFolderPath = string.Empty;
        }
        using var dialog = new ProjectsFolderHistoryDialog(
            _workspaceService.GetRecentProjectsFolderPaths(),
            currentFolderPath,
            _workspaceService.RemoveRecentProjectsFolderPath);

        if (dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedFolderPath))
        {
            RefreshSharedPathDisplays();
            return;
        }

        var selectedPath = dialog.SelectedFolderPath;
        var currentProjectId = _currentProject?.ProjectId;
        if (_currentProject is not null && !ConfirmUnsavedChangesBeforeProceeding())
        {
            return;
        }
        try
        {
            _workspaceService.ReleaseCurrentProject();
        }
        catch (Exception ex) when (IsSharedProjectError(ex))
        {
            ShowSharedProjectError(ex);
            return;
        }

        _currentProject = null;
        _currentEntry = null;
        UpdateAutosaveLabel();

        _workspaceService.SetProjectsFolderPath(selectedPath);
        UpdateProjectsFolderDisplay();

        var previousProjectId = currentProjectId;
        IReadOnlyList<TrackingProject> projects;
        try
        {
            projects = _workspaceService.GetProjects();
        }
        catch (Exception ex) when (IsSharedProjectError(ex))
        {
            ShowSharedProjectError(ex);
            return;
        }
        var projectToLoad = projects.FirstOrDefault(project => project.ProjectId == previousProjectId) ?? projects.FirstOrDefault();

        _currentEntry = null;

        if (projectToLoad is not null)
        {
            try
            {
                var opened = _workspaceService.OpenProject(projectToLoad.ProjectId);
                if (opened is not null)
                {
                    LoadProject(opened);
                    HandleMergeAfterOpen();
                }
            }
            catch (Exception ex) when (IsSharedProjectError(ex))
            {
                ShowSharedProjectError(ex);
                _currentProject = null;
                UpdateAutosaveLabel();
            }
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
            $"Carpeta de proyectos actualizada:\n{selectedPath}",
            "Carpeta de proyectos",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void RefreshCurrentProjectButton_Click(object? sender, EventArgs e)
    {
        var projectId = _currentProject?.ProjectId ?? (_projectSelector.SelectedItem as ProjectSelectorItem)?.ProjectId;
        if (projectId is null || !ConfirmCurrentEntryChanges()) return;
        try
        {
            var selectedEntryId = _currentEntry?.EntryId;
            var project = _workspaceService.OpenProject(projectId.Value);
            if (project is null) return;
            _currentEntry = null;
            LoadProject(project);
            HandleMergeAfterOpen(selectedEntryId);
        }
        catch (Exception ex) when (IsSharedProjectError(ex))
        {
            ShowSharedProjectError(ex);
        }
    }

    private string? ResolvePathWithRecovery(string? storedPath, bool directory)
    {
        if (string.IsNullOrWhiteSpace(storedPath)) return null;
        var resolved = _sharedPathService.Resolve(storedPath, directory);
        if (resolved is not null && (directory ? Directory.Exists(resolved) : File.Exists(resolved)))
            return resolved;

        var pathType = directory ? "carpeta" : "archivo MD";
        if (MessageBox.Show(this,
                $"No se encuentra la {pathType} en este PC:\n{storedPath}\n\n" +
                "Se ha intentado adaptar la ruta al perfil de Windows actual. " +
                "¿Quieres elegir una carpeta raíz para buscar una carpeta con el mismo nombre y reconstruir la ruta?",
                "Localizar ruta compartida", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return null;

        var currentRoot = _sharedPathService.GetLocalRoot();
        using var dialog = new FolderBrowserDialog
        {
            Description = "Selecciona una carpeta que sea parte de la ruta original o que contenga una de sus carpetas. Se comprobará la ruta completa antes de usarla.",
            UseDescriptionForTitle = true,
            SelectedPath = !string.IsNullOrWhiteSpace(currentRoot) && Directory.Exists(currentRoot)
                ? currentRoot : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return null;

        var matches = _sharedPathService.FindMatches(storedPath, dialog.SelectedPath, directory);
        if (matches.Count == 0)
        {
            MessageBox.Show(this,
                "No se ha encontrado una ruta completa bajo la carpeta elegida. Selecciona una carpeta más cercana al destino y vuelve a intentarlo.",
                "Ruta no encontrada", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        SharedPathMatch selected;
        if (matches.Count == 1)
        {
            selected = matches[0];
        }
        else
        {
            using var selection = new SharedPathMatchDialog(matches);
            if (selection.ShowDialog(this) != DialogResult.OK || selection.SelectedMatch is null) return null;
            selected = selection.SelectedMatch;
        }

        _sharedPathService.RememberMatch(selected);
        RefreshSharedPathDisplays();
        return selected.ResolvedPath;
    }

    private static bool IsSharedProjectError(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or InvalidOperationException or JsonException;

    private void ShowSharedProjectError(Exception ex)
    {
        var message = ex.Message;
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current.HResult is not (unchecked((int)0x8007017C) or unchecked((int)0x8007016A))) continue;
            if (!message.Contains("Mantener siempre en este dispositivo", StringComparison.OrdinalIgnoreCase))
                message += "\n\nOneDrive no permite leer un archivo sincronizado. Marca la carpeta del proyecto como " +
                           "'Mantener siempre en este dispositivo', espera a que aparezca disponible y vuelve a intentarlo. " +
                           "No borres el archivo de cambios.";
            break;
        }
        MessageBox.Show(this, message, "Proyecto compartido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void RefreshSharedPathDisplays()
    {
        if (_currentEntry is null)
        {
            return;
        }

        var inputPath = GetFieldValue(_currentEntry, InputReceivedPathKey);
        var markdownPath = GetFieldValue(_currentEntry, MarkdownFilePathKey);
        UpdateInputReceivedPathDisplay(_sharedPathService.Resolve(inputPath, directory: true) ?? inputPath);
        UpdateMarkdownFilePathDisplay(_sharedPathService.Resolve(markdownPath) ?? markdownPath);
    }

    private void DailyTrackingForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!ConfirmUnsavedChangesBeforeProceeding())
        {
            e.Cancel = true;
            return;
        }

        if (_currentProject is not null)
        {
            try
            {
                var selectedEntryId = _currentEntry?.EntryId;
                var refreshed = _workspaceService.OpenProject(_currentProject.ProjectId);
                if (refreshed is not null && _workspaceService.LastMergeResult is { } merged)
                {
                    var incoming = merged.IncorporatedChanges;
                    merged = ResolveConflicts(merged);
                    _currentProject = merged.Project;
                    _currentEntry = merged.Project.Entries.FirstOrDefault(entry => entry.EntryId == selectedEntryId);
                    ShowMergeNotice(incoming);
                }
            }
            catch (Exception ex) when (IsSharedProjectError(ex))
            {
                ShowSharedProjectError(ex);
                e.Cancel = true;
                return;
            }
        }

        _workspaceService.CreateExitBackup(_currentProject);
        try
        {
            _workspaceService.ReleaseCurrentProject();
            _presenceTimer.Stop();
            _presenceTimer.Dispose();
        }
        catch (Exception ex) when (IsSharedProjectError(ex))
        {
            ShowSharedProjectError(ex);
            e.Cancel = true;
        }
    }

    private void EntriesListView_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_suppressEvents || _currentProject is null || _entriesListView.SelectedItems.Count == 0)
        {
            return;
        }

        if (_entriesListView.SelectedItems[0].Tag is Guid entryId)
        {
            if (_currentEntry?.EntryId == entryId) return;
            if (!ConfirmCurrentEntryChanges())
            {
                SelectCurrentEntryInList();
                return;
            }
            var entry = _currentProject.Entries.FirstOrDefault(item => item.EntryId == entryId);

            if (entry is not null)
            {
                LoadEntry(entry);
            }
        }
    }

    private bool ConfirmCurrentEntryChanges()
    {
        CommitPendingLongTextChanges();
        if (!_hasUnsavedChanges) return true;

        var answer = MessageBox.Show(this,
            "Hay cambios sin guardar en el registro. ¿Quieres guardarlos antes de continuar?",
            "Guardar registro", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
        if (answer == DialogResult.Cancel) return false;
        if (answer == DialogResult.Yes) return SaveCurrentProject();

        if (_currentProject is null) return true;
        try
        {
            var reloaded = _workspaceService.OpenProject(_currentProject.ProjectId);
            if (reloaded is null) return false;
            _currentEntry = null;
            _hasUnsavedChanges = false;
            _pendingLongTextChanges = false;
            LoadProject(reloaded, createInitialEntry: false);
            if (reloaded.Entries.Count > 0) HandleMergeAfterOpen();
            return true;
        }
        catch (Exception ex) when (IsSharedProjectError(ex))
        {
            ShowSharedProjectError(ex);
            return false;
        }
    }

    private static void EntriesListView_DrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
    {
        e.DrawDefault = true;
    }

    private static void EntriesListView_DrawItem(object? sender, DrawListViewItemEventArgs e)
    {
        e.DrawDefault = true;
    }

    private void EntriesListView_DrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
    {
        if (e.ColumnIndex is not 0 and not 1 || e.SubItem is null)
        {
            e.DrawDefault = true;
            return;
        }

        e.DrawBackground();

        if (e.ColumnIndex == 0)
        {
            DrawStatusBadge(e.Graphics, e.Bounds, e.SubItem.Text);
            return;
        }

        DrawPriorityIndicator(e.Graphics, e.Bounds, e.SubItem.Text);
    }

    private void DrawStatusBadge(Graphics graphics, Rectangle bounds, string status)
    {
        const int sectorCount = 5;
        const float sectorAngle = 360F / sectorCount;
        var diameter = Math.Min(22, Math.Min(bounds.Width - 8, bounds.Height - 6));

        if (diameter <= 0)
        {
            return;
        }

        var badgeBounds = new Rectangle(
            bounds.Left + ((bounds.Width - diameter) / 2),
            bounds.Top + ((bounds.Height - diameter) / 2),
            diameter,
            diameter);
        var badgeColor = _statusFieldDefinition is null
            ? Color.Gainsboro
            : OptionColorHelper.GetColor(_statusFieldDefinition, status);
        var filledSectors = StatusRanks.TryGetValue(status, out var rank) ? rank : 0;

        var previousSmoothingMode = graphics.SmoothingMode;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        using (var emptyBrush = new SolidBrush(Color.FromArgb(232, 236, 240)))
        using (var badgeBrush = new SolidBrush(badgeColor))
        using (var separatorPen = new Pen(Color.White, 1F))
        using (var borderPen = new Pen(Color.FromArgb(110, 120, 130)))
        {
            graphics.FillEllipse(emptyBrush, badgeBounds);
            for (var sector = 0; sector < filledSectors; sector++)
            {
                graphics.FillPie(badgeBrush, badgeBounds, -90F + sector * sectorAngle, sectorAngle);
            }

            var centerX = badgeBounds.Left + badgeBounds.Width / 2F;
            var centerY = badgeBounds.Top + badgeBounds.Height / 2F;
            var radius = badgeBounds.Width / 2F;
            for (var sector = 0; sector < sectorCount; sector++)
            {
                var angle = (-90F + sector * sectorAngle) * Math.PI / 180D;
                graphics.DrawLine(separatorPen, centerX, centerY,
                    centerX + (float)(Math.Cos(angle) * radius),
                    centerY + (float)(Math.Sin(angle) * radius));
            }

            graphics.DrawEllipse(borderPen, badgeBounds);
        }

        graphics.SmoothingMode = previousSmoothingMode;
    }

    private void DrawPriorityIndicator(Graphics graphics, Rectangle bounds, string priority)
    {
        if (!PriorityIndicators.TryGetValue(priority, out var indicator))
        {
            return;
        }

        var indicatorColor = _priorityFieldDefinition is null
            ? Color.Gainsboro
            : OptionColorHelper.GetColor(_priorityFieldDefinition, priority);
        using var indicatorFont = new Font(_entriesListView.Font.FontFamily, 17F, FontStyle.Bold, GraphicsUnit.Point);
        TextRenderer.DrawText(
            graphics,
            indicator,
            indicatorFont,
            bounds,
            indicatorColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    private void EntriesListView_Resize(object? sender, EventArgs e)
    {
        if (_entriesListView.Columns.Count != 7)
        {
            return;
        }

        var availableWidth = Math.Max(_entriesListView.ClientSize.Width - 8, 260);
        _entriesListView.Columns[0].Width = 36;
        _entriesListView.Columns[1].Width = 30;
        _entriesListView.Columns[2].Width = 50;
        _entriesListView.Columns[3].Width = 95;

        var remainingWidth = Math.Max(availableWidth - 211, 300);
        var columnWidth = Math.Max(remainingWidth / 3, 100);
        _entriesListView.Columns[4].Width = columnWidth;
        _entriesListView.Columns[5].Width = columnWidth;
        _entriesListView.Columns[6].Width = remainingWidth - (columnWidth * 2);
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
