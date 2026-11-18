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

    [Fact]
    public void Paint_DrawsTheClosedSpanAsADottedLineAfterTheSolidOne()
    {
        RunOnStaThread(() =>
        {
            var status = new TrackingFieldDefinition
            {
                Key = "status",
                OptionColors = new Dictionary<string, string> { ["Cerrado"] = "#34495E" }
            };
            // The axis starts on Monday 5 Oct, so the solid part spans x 119-183 and the dotted part x 183-339 on the row centered at y 119.
            var row = new TrackingTimelineRow(
                1,
                new DateTime(2026, 10, 5, 8, 0, 0),
                new DateTime(2026, 10, 5, 17, 0, 0),
                "Cerrado", "Registro #1", [],
                new DateTime(2026, 10, 7, 17, 0, 0));
            using var panel = new TrackingTimelinePanel { Size = new Size(600, 280) };
            panel.SetData([row], status);
            panel.CreateControl();
            using var bitmap = new Bitmap(panel.Width, panel.Height);
            panel.DrawToBitmap(bitmap, new Rectangle(Point.Empty, panel.Size));

            var lineColor = Color.FromArgb(52, 73, 94).ToArgb();
            var rowBackground = Color.FromArgb(247, 249, 251).ToArgb();

            Assert.All(Enumerable.Range(130, 46), x => Assert.Equal(lineColor, bitmap.GetPixel(x, 119).ToArgb()));

            var dottedPixels = Enumerable.Range(200, 131).Select(x => bitmap.GetPixel(x, 119).ToArgb()).ToList();
            Assert.True(dottedPixels.Count(pixel => pixel == lineColor) > 20, "the dotted span has no dots");
            Assert.True(dottedPixels.Count(pixel => pixel == rowBackground) > 20, "the dotted span has no gaps");

            Assert.Equal(rowBackground, bitmap.GetPixel(355, 119).ToArgb());
        });
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
