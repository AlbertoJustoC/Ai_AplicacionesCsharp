using Ai_DailyTracking.Shared.Helpers;

namespace Ai_DailyTracking.UI.Forms;

// Asks for the visible title of a link about to be inserted in the notes; the link itself comes from the clipboard.
public sealed class InsertLinkDialog : Form
{
    private readonly TextBox _titleTextBox;

    public InsertLinkDialog(NoteLink link, string suggestedTitle)
    {
        Text = "Insertar enlace";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(480, 238);
        BackColor = Color.FromArgb(245, 247, 250);
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);

        var titleLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 44,
            Text = $"Insertar enlace: {NoteLinkHelper.DisplayName(link.Kind)}",
            Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold, GraphicsUnit.Point),
            Padding = new Padding(20, 14, 20, 0)
        };

        var urlTextBox = new TextBox
        {
            Left = 20,
            Top = 56,
            Width = 434,
            ReadOnly = true,
            Text = link.Url,
            BackColor = Color.FromArgb(236, 240, 244)
        };

        var captionLabel = new Label
        {
            Left = 20,
            Top = 94,
            Width = 434,
            Height = 22,
            Text = "Texto que se mostrara en la nota",
            ForeColor = Color.FromArgb(88, 96, 110)
        };

        _titleTextBox = new TextBox
        {
            Left = 20,
            Top = 118,
            Width = 434,
            Text = suggestedTitle,
            PlaceholderText = NoteLinkHelper.DefaultTitle(link.Kind)
        };
        _titleTextBox.SelectAll();

        var buttonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12)
        };

        var insertButton = new Button
        {
            Text = "Insertar",
            AutoSize = true,
            BackColor = Color.FromArgb(18, 103, 177),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Padding = new Padding(12, 6, 12, 6),
            DialogResult = DialogResult.OK
        };
        insertButton.FlatAppearance.BorderSize = 0;

        var cancelButton = new Button
        {
            Text = "Cancelar",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            Padding = new Padding(12, 6, 12, 6),
            DialogResult = DialogResult.Cancel
        };

        buttonsPanel.Controls.Add(insertButton);
        buttonsPanel.Controls.Add(cancelButton);

        Controls.Add(buttonsPanel);
        Controls.Add(_titleTextBox);
        Controls.Add(captionLabel);
        Controls.Add(urlTextBox);
        Controls.Add(titleLabel);

        AcceptButton = insertButton;
        CancelButton = cancelButton;
        ActiveControl = _titleTextBox;
    }

    public string LinkTitle => _titleTextBox.Text.Trim();
}
