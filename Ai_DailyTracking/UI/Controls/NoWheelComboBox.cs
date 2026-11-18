using System.Runtime.InteropServices;

namespace Ai_DailyTracking.UI.Controls;

// ComboBox that ignores the mouse wheel while its list is closed, so scrolling over it cannot change the value by accident.
// The value only changes by opening the list or typing, as with the editable-option fields.
public sealed class NoWheelComboBox : ComboBox
{
    private const int WmMouseWheel = 0x020A;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmMouseWheel && !DroppedDown)
        {
            // Hand the wheel to the parent so a scrollable container still scrolls, as it does over a label.
            if (Parent is { IsHandleCreated: true } parent)
            {
                SendMessage(parent.Handle, m.Msg, m.WParam, m.LParam);
            }

            m.Result = IntPtr.Zero;
            return;
        }

        base.WndProc(ref m);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
}
