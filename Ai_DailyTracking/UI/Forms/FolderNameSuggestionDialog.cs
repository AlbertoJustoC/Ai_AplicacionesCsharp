using Ai_DailyTracking.Shared.Helpers;

namespace Ai_DailyTracking.UI.Forms;

public sealed class FolderNameSuggestionDialog : Form
{
    private readonly TextBox _prefixTextBox = new();
    private readonly TextBox _proposalTextBox = new();
    private readonly int _entryNumber;
    private readonly string _area;
    private readonly string _package;
    private readonly string? _extension;

    public string Prefix { get; private set; } = string.Empty;
    public string ProposedName { get; private set; } = string.Empty;

    public FolderNameSuggestionDialog(
        string title,
        string helpText,
        string initialPrefix,
        int entryNumber,
        string area,
        string package,
        string? extension)
    {
        _entryNumber = entryNumber;
        _area = area;
        _package = package;
        _extension = extension;

        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(460, 224);
        BackColor = Color.FromArgb(245, 247, 250);
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);

        var helpLabel = new Label
        {
            Left = 20,
            Top = 14,
            Width = 420,
            Height = 40,
            Text = helpText,
            ForeColor = Color.FromArgb(88, 96, 110)
        };

        var prefixLabel = new Label
        {
            Left = 20,
            Top = 62,
            Width = 420,
            Text = "Prefijo (opcional):"
        };

        _prefixTextBox.Left = 20;
        _prefixTextBox.Top = 86;
        _prefixTextBox.Width = 420;
        _prefixTextBox.Text = initialPrefix;
        _prefixTextBox.TextChanged += (_, _) => UpdateProposal();

        var proposalLabel = new Label
        {
            Left = 20,
            Top = 120,
            Width = 420,
            Text = "Propuesta:"
        };

        _proposalTextBox.Left = 20;
        _proposalTextBox.Top = 144;
        _proposalTextBox.Width = 420;
        _proposalTextBox.ReadOnly = true;
        _proposalTextBox.BackColor = Color.White;

        var buttonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12)
        };

        var copyButton = new Button
        {
            Text = "Copiar",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            Padding = new Padding(12, 6, 12, 6)
        };
        copyButton.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(ProposedName))
            {
                Clipboard.SetText(ProposedName);
            }
            DialogResult = DialogResult.OK;
            Close();
        };

        buttonsPanel.Controls.Add(copyButton);

        Controls.Add(buttonsPanel);
        Controls.Add(_proposalTextBox);
        Controls.Add(proposalLabel);
        Controls.Add(_prefixTextBox);
        Controls.Add(prefixLabel);
        Controls.Add(helpLabel);

        AcceptButton = copyButton;

        UpdateProposal();
    }

    private void UpdateProposal()
    {
        Prefix = _prefixTextBox.Text.Trim();
        var safeArea = FileNameSanitizer.SanitizePart(_area, "Area");
        var safePackage = FileNameSanitizer.SanitizePart(_package, "Paquete");
        var core = $"{_entryNumber:0000}-{safeArea}-{safePackage}";
        var name = string.IsNullOrEmpty(Prefix) ? core : $"{Prefix}.{core}";
        ProposedName = _extension is null ? name : name + _extension;
        _proposalTextBox.Text = ProposedName;
    }
}
