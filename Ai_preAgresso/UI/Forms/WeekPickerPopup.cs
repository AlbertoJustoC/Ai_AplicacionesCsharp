using Ai_preAgresso.Application.Services;

namespace Ai_preAgresso.UI.Forms;

// Small popup that lets the user pick a week visually; clicking the week-number gutter selects the whole Mon-Sun range.
public sealed class WeekPickerPopup : Form
{
    private readonly MonthCalendar _calendar = new()
    {
        FirstDayOfWeek = Day.Monday,
        ShowWeekNumbers = true,
        MaxSelectionCount = 7
    };

    public DateOnly SelectedMonday { get; private set; }

    public WeekPickerPopup(DateOnly initialDate)
    {
        Text = "Elegir semana";
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MaximizeBox = false;
        MinimizeBox = false;

        SelectedMonday = initialDate;

        _calendar.SetDate(initialDate.ToDateTime(TimeOnly.MinValue));
        _calendar.DateSelected += Calendar_DateSelected;

        Controls.Add(_calendar);
        ClientSize = _calendar.Size;
    }

    private void Calendar_DateSelected(object? sender, DateRangeEventArgs e)
    {
        var picked = DateOnly.FromDateTime(e.Start);
        SelectedMonday = WeekPeriodCalculator.GetMonday(
            WeekPeriodCalculator.GetIsoYear(picked),
            WeekPeriodCalculator.GetIsoWeek(picked));
        DialogResult = DialogResult.OK;
        Close();
    }
}
