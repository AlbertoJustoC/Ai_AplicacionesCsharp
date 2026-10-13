using System.Drawing;
using Ai_DailyTracking.Application.Services;
using Ai_DailyTracking.Domain.Models;
using Ai_DailyTracking.UI.Controls;

namespace Ai_DailyTracking.Tests;

public sealed class TrackingTimelinePanelTests
{
    [Fact]
    public void Paint_DrawsTheRecordLineInItsCurrentStatusColor()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var status = new TrackingFieldDefinition
                {
                    Key = "status",
                    OptionColors = new Dictionary<string, string> { ["En curso"] = "#2E86C1" }
                };
                var row = new TrackingTimelineRow(
                    1,
                    new DateTime(2026, 10, 5, 8, 0, 0),
                    new DateTime(2026, 10, 6, 17, 0, 0),
                    "En curso", "Registro #1", []);
                using var panel = new TrackingTimelinePanel { Size = new Size(600, 280) };
                panel.SetData([row], status);
                panel.CreateControl();
                using var bitmap = new Bitmap(panel.Width, panel.Height);
                panel.DrawToBitmap(bitmap, new Rectangle(Point.Empty, panel.Size));

                Assert.Equal(Color.FromArgb(46, 134, 193).ToArgb(), bitmap.GetPixel(150, 119).ToArgb());
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
