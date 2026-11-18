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
        Assert.Equal(new DateTime(2026, 10, 6, 8, 0, 0), rows[0].Start);
        Assert.Equal(new DateTime(2026, 10, 7, 17, 0, 0), rows[0].End);
        Assert.Equal(new DateTime(2026, 10, 8, 7, 20, 0), rows[1].Start);
        Assert.Equal(new DateTime(2026, 10, 8, 19, 40, 0), rows[1].End);
        Assert.Equal(3, rows[1].Notes.Count);
        Assert.Equal(new DateTime(2026, 10, 8, 7, 20, 0), rows[1].Notes[0].Timestamp);
        Assert.Contains("Segunda" + Environment.NewLine + "continúa", rows[1].Notes[1].Text);
        Assert.Contains("Estado: En curso", rows[1].Summary);
    }

    [Fact]
    public void Build_StartsTheLineAtTheFirstNoteInsteadOfTheFechaField()
    {
        var row = BuildRow("En curso", "[2026-10-05 11:00] **En curso**\n[2026-10-02 09:30] **Nuevo**", recordDate: "2026-10-05", updatedDate: "2026-10-05");

        Assert.Equal(new DateTime(2026, 10, 2, 9, 30, 0), row.Start);
        Assert.Equal(new DateTime(2026, 10, 5, 11, 0, 0), row.End);
        Assert.Null(row.ClosedAt);
    }

    [Fact]
    public void Build_EndsTheSolidLineWhenTheStatusBecomesDoneEvenIfTheRecordWasEditedLater()
    {
        var row = BuildRow(
            "Hecho",
            "[2026-10-02 09:00] **Nuevo**\n[2026-10-02 15:00] **En curso**\n[2026-10-05 11:00] **Hecho**",
            updatedDate: "2026-10-30");

        Assert.Equal(new DateTime(2026, 10, 2, 9, 0, 0), row.Start);
        Assert.Equal(new DateTime(2026, 10, 5, 11, 0, 0), row.End);
        Assert.Null(row.ClosedAt);
    }

    [Fact]
    public void Build_EndsTheSolidLineAtTheNoteThatLastMadeTheRecordDone()
    {
        var row = BuildRow(
            "Hecho",
            "[2026-10-02 10:00] **Hecho**\n[2026-10-02 16:00] **En revisión**\n[2026-10-08 12:00] **Hecho**\n[2026-10-08 12:30] **Hecho** entregado");

        Assert.Equal(new DateTime(2026, 10, 8, 12, 0, 0), row.End);
        Assert.Null(row.ClosedAt);
    }

    [Fact]
    public void Build_ContinuesAClosedRecordAsADottedSpanUntilTheClosingNote()
    {
        var row = BuildRow(
            "Cerrado",
            "[2026-10-26 09:00] **Nuevo**\n[2026-10-26 12:00] **Hecho**\n[2026-10-27 10:00] **Cerrado**",
            updatedDate: "2026-10-30");

        Assert.Equal(new DateTime(2026, 10, 26, 9, 0, 0), row.Start);
        Assert.Equal(new DateTime(2026, 10, 26, 12, 0, 0), row.End);
        Assert.Equal(new DateTime(2026, 10, 27, 10, 0, 0), row.ClosedAt);
    }

    [Fact]
    public void Build_ClosedRecord_EndsTheSolidLineWhenItBecameDoneNotAtLaterDoneComments()
    {
        var row = BuildRow(
            "Cerrado",
            "[2026-10-26 07:52] **Nuevo**\n[2026-10-26 15:48] **Hecho**\n[2026-10-27 08:36] **Hecho** comentario\n[2026-10-27 08:36] **Cerrado**");

        Assert.Equal(new DateTime(2026, 10, 26, 15, 48, 0), row.End);
        Assert.Equal(new DateTime(2026, 10, 27, 8, 36, 0), row.ClosedAt);
    }

    [Fact]
    public void Build_ClosedWithoutDone_KeepsTheSolidLineUntilTheLastNoteBeforeClosing()
    {
        var row = BuildRow(
            "Cerrado",
            "[2026-10-05 09:00] **Nuevo**\n[2026-10-08 10:00] **En revisión**\n[2026-10-09 16:00] **Cerrado**");

        Assert.Equal(new DateTime(2026, 10, 8, 10, 0, 0), row.End);
        Assert.Equal(new DateTime(2026, 10, 9, 16, 0, 0), row.ClosedAt);
    }

    [Fact]
    public void Build_NotesWithoutAStatusMarkerKeepThePreviousStatus()
    {
        var row = BuildRow(
            "Cerrado",
            "[2026-10-26 09:00] **Hecho**\n[2026-10-27 10:00] **Cerrado**\n[2026-10-27 15:00] se archivó");

        Assert.Equal(new DateTime(2026, 10, 26, 9, 0, 0), row.End);
        Assert.Equal(new DateTime(2026, 10, 27, 15, 0, 0), row.ClosedAt);
    }

    [Fact]
    public void Build_RecordNotDoneYetEndsAtTheLastChange()
    {
        var row = BuildRow("En curso", "[2026-10-05 09:00] **Nuevo**\n[2026-10-08 10:00] **En curso**", updatedDate: "2026-10-16");

        Assert.Equal(new DateTime(2026, 10, 16, 17, 0, 0), row.End);
        Assert.Null(row.ClosedAt);
    }

    [Theory]
    [InlineData("Hecho")]
    [InlineData("Cerrado")]
    public void Build_FinishedRecordWhoseNotesDoNotShowItEndsAtTheLastChange(string status)
    {
        var row = BuildRow(status, "[2026-10-05 09:00] **Nuevo**", updatedDate: "2026-10-09");

        Assert.Equal(new DateTime(2026, 10, 9, 17, 0, 0), row.End);
        Assert.Null(row.ClosedAt);
    }

    [Fact]
    public void ParseNotes_ReadsOnlyTheMarkerThatLeadsTheNote()
    {
        var notes = TrackingTimelineBuilder.ParseNotes(
            "[2026-10-02 09:00] **Hecho** revisar **esto**\n[2026-10-03 09:00] texto **Cerrado** al final\n[2026-10-04 09:00] **En revisión**\ncontinúa");

        Assert.Equal(["Hecho", null, "En revisión"], notes.Select(note => note.Status));
        Assert.Contains("continúa", notes[2].Text);
    }

    private static readonly TrackingFormSchema Schema = new()
    {
        Fields =
        [
            new() { Key = "recordDate", Label = "Fecha", Type = TrackingFieldType.Date },
            new() { Key = "updatedDate", Label = "Último cambio", Type = TrackingFieldType.Date },
            new() { Key = "status", Label = "Estado", Type = TrackingFieldType.Option },
            new() { Key = "notes", Label = "Notas", Type = TrackingFieldType.LongText }
        ]
    };

    private static TrackingTimelineRow BuildRow(string status, string notes, string recordDate = "2026-10-05", string updatedDate = "2026-10-30")
    {
        var entry = new TrackingEntry
        {
            EntryNumber = 1,
            Values = new Dictionary<string, string?>
            {
                ["recordDate"] = recordDate,
                ["updatedDate"] = updatedDate,
                ["status"] = status,
                ["notes"] = notes
            }
        };

        return TrackingTimelineBuilder.Build([entry], Schema, "recordDate").Single();
    }
}
