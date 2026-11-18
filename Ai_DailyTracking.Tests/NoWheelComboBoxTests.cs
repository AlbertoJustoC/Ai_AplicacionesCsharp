using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Ai_DailyTracking.UI.Controls;

namespace Ai_DailyTracking.Tests;

public sealed class NoWheelComboBoxTests
{
    private const int WmMouseWheel = 0x020A;

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? className, string? title);

    [Theory]
    [InlineData(ComboBoxStyle.DropDownList, false)]
    [InlineData(ComboBoxStyle.DropDown, false)]
    [InlineData(ComboBoxStyle.DropDown, true)]
    public void MouseWheel_DoesNotChangeTheSelection(ComboBoxStyle style, bool overTheTextArea)
    {
        RunOnStaThread(() =>
        {
            // A plain ComboBox does move to the next item, which proves the message sent here is the one that bites.
            Assert.Equal(2, SelectedIndexAfterWheel(new ComboBox(), style, overTheTextArea));
            Assert.Equal(1, SelectedIndexAfterWheel(new NoWheelComboBox(), style, overTheTextArea));
        });
    }

    [Fact]
    public void MouseWheel_StillScrollsTheContainerHoldingTheCombo()
    {
        RunOnStaThread(() =>
        {
            Assert.Equal(0, ScrollPositionAfterWheel(new ComboBox()));
            Assert.True(ScrollPositionAfterWheel(new NoWheelComboBox()) < 0);
        });
    }

    private static int SelectedIndexAfterWheel(ComboBox combo, ComboBoxStyle style, bool overTheTextArea)
    {
        using var form = new Form();
        var parent = new Panel { Dock = DockStyle.Fill };
        PrepareCombo(combo, style);
        parent.Controls.Add(combo);
        form.Controls.Add(parent);
        CreateHandlesTopDown(form, parent, combo);

        var target = overTheTextArea ? FindWindowEx(combo.Handle, IntPtr.Zero, "Edit", null) : combo.Handle;
        Assert.NotEqual(IntPtr.Zero, target);
        TurnWheelDown(target);
        return combo.SelectedIndex;
    }

    // Y of the auto-scroll position of a tall container after the wheel turns over a combo nested two levels below it.
    private static int ScrollPositionAfterWheel(ComboBox combo)
    {
        using var form = new Form { Size = new Size(400, 300) };
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var content = new Panel { Size = new Size(300, 1200) };
        var card = new Panel { Location = new Point(10, 10), Size = new Size(250, 80) };
        PrepareCombo(combo, ComboBoxStyle.DropDownList);
        card.Controls.Add(combo);
        content.Controls.Add(card);
        scroll.Controls.Add(content);
        form.Controls.Add(scroll);
        CreateHandlesTopDown(form, scroll, content, card, combo);
        scroll.PerformLayout();

        TurnWheelDown(combo.Handle);
        return scroll.AutoScrollPosition.Y;
    }

    private static void PrepareCombo(ComboBox combo, ComboBoxStyle style)
    {
        combo.DropDownStyle = style;
        combo.Items.AddRange(["A", "B", "C", "D"]);
        combo.SelectedIndex = 1;
    }

    // Handles must exist top-down, as they do on a shown form, or a child gets parked outside its parent.
    private static void CreateHandlesTopDown(params Control[] controls)
    {
        foreach (var control in controls)
        {
            _ = control.Handle;
        }
    }

    private static void TurnWheelDown(IntPtr target)
    {
        const int wheelDelta = -120;
        SendMessage(target, WmMouseWheel, new IntPtr((wheelDelta & 0xFFFF) << 16), new IntPtr((50 << 16) | 50));
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;
    }
}
