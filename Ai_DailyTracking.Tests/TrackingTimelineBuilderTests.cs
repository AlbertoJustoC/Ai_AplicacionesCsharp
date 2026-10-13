using Ai_DailyTracking.Application.Services;
using Ai_DailyTracking.Domain.Models;

namespace Ai_DailyTracking.Tests;

public sealed class TrackingTimelineBuilderTests
{
    [Theory]
    [InlineData(6, 30, 0)]
    [InlineData(8, 0, 0)]
    [InlineData(12, 30, 0.5)]
    [InlineData(17, 0, 1)]
    [InlineData(19, 15, 1)]
    public void DayFraction_ClampsToTheVisibleWorkingDay(int hour, int minute, double expected)
    {
        Assert.Equal(expected, TrackingTimelineBuilder.DayFraction(new DateTime(2026, 10, 9, hour, minute, 0)), 8);
    }

    [Fact]
    public void Build_OrdersRowsAndPreservesNotesAtTheirActualTimes()
    {
        var schema = new TrackingFormSchema
        {
            Fields =
            [
                new() { Key = "recordDate", Label = "Fecha", Type = TrackingFieldType.Date },
                new() { Key = "updatedDate", Label = "Último cambio", Type = TrackingFieldType.Date },
                new() { Key = "status", Label = "Estado", Type = TrackingFieldType.Option },
                new() { Key = "notes", Label = "Notas", Type = TrackingFieldType.LongText }
            ]
        };
        var later = new TrackingEntry
        {
            EntryNumber = 2,
            Values = new Dictionary<string, string?>
            {
                ["recordDate"] = "2026-10-05",
                ["updatedDate"] = "2026-10-08",
                ["status"] = "En curso",
                ["notes"] = "[2026-10-08 07:20] Primera\n[2026-10-08 12:30] Segunda\ncontinúa\n[2026-10-08 19:40] Tercera"
            }
        };
        var earlier = new TrackingEntry
        {
            EntryNumber = 1,
            Values = new Dictionary<string, string?> { ["recordDate"] = "2026-10-06", ["updatedDate"] = "2026-10-07" }
        };

        var rows = TrackingTimelineBuilder.Build([later, earlier], schema, "recordDate");

        Assert.Equal([1, 2], rows.Select(row => row.EntryNumber));
        Assert.Equal(new DateTime(2026, 10, 5, 8, 0, 0), rows[1].Start);
        Assert.Equal(new DateTime(2026, 10, 8, 19, 40, 0), rows[1].End);
        Assert.Equal(3, rows[1].Notes.Count);
        Assert.Equal(new DateTime(2026, 10, 8, 7, 20, 0), rows[1].Notes[0].Timestamp);
        Assert.Contains("Segunda" + Environment.NewLine + "continúa", rows[1].Notes[1].Text);
        Assert.Contains("Estado: En curso", rows[1].Summary);
    }
}
