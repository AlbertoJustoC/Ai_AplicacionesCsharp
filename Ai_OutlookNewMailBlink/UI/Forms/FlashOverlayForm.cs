using Ai_OutlookNewMailBlink.Application.Services;
using Ai_OutlookNewMailBlink.Shared.Helpers;

namespace Ai_OutlookNewMailBlink.UI.Forms;

// Small always-on-top window shown in a screen corner as a fallback alert when flashing
// Outlook's own window isn't reliable (e.g. new Outlook not responding well to FlashWindowEx).
public sealed class FlashOverlayForm : Form
{
    private readonly System.Windows.Forms.Timer _blinkTimer;
    private readonly Label _messageLabel;
    private readonly WindowFlashService _flashService;
    private bool _blinkOn;

    public FlashOverlayForm(WindowFlashService flashService)
    {
        _flashService = flashService;

        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = true;
        TopMost = true;
        Width = 260;
        Height = 70;
        Text = "Nuevos correos";
        Icon = AppIconProvider.Current;
        BackColor = Color.OrangeRed;

        _messageLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font(FontFamily.GenericSansSerif, 12, FontStyle.Bold),
            ForeColor = Color.White,
            Text = "Nuevos correos"
        };
        Controls.Add(_messageLabel);

        _messageLabel.Click += (_, _) => Dismiss();
        Click += (_, _) => Dismiss();

        _blinkTimer = new System.Windows.Forms.Timer { Interval = 500 };
        _blinkTimer.Tick += (_, _) => ToggleBlinkColor();

        PositionInBottomRightCorner();
    }

    public void ShowAndFlash(int unreadCount)
    {
        _messageLabel.Text = $"Nuevos correos ({unreadCount})";
        PositionInBottomRightCorner();

        if (!Visible)
        {
            Show();
        }

        _blinkTimer.Start();
        _flashService.StartFlashing(Handle);
    }

    private void Dismiss()
    {
        _blinkTimer.Stop();
        _flashService.StopFlashing(Handle);
        BackColor = Color.OrangeRed;
        Hide();
    }

    private void ToggleBlinkColor()
    {
        _blinkOn = !_blinkOn;
        BackColor = _blinkOn ? Color.OrangeRed : Color.DarkRed;
    }

    private void PositionInBottomRightCorner()
    {
        var workingArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        Location = new Point(workingArea.Right - Width - 16, workingArea.Bottom - Height - 16);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // The tray app keeps this form alive for reuse - hide instead of disposing on user close.
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Dismiss();
            return;
        }

        base.OnFormClosing(e);
    }
}
