namespace Ai_DailyTracking.Domain.Models;

// One printable row of the PDF report table, already formatted for display.
public sealed record DailyTrackingReportRow(int EntryNumber, string DateText, string Headline, string Status);
