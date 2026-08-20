using System.Globalization;

namespace Ai_preAgresso.Application.Services;

public static class WeekPeriodCalculator
{
    private static readonly CultureInfo SpanishCulture = CultureInfo.GetCultureInfo("es-ES");

    public static IReadOnlyList<string> NombresMeses { get; } =
        Enumerable.Range(1, 12).Select(GetNombreMes).ToList();

    public static int GetIsoYear(DateOnly date) => ISOWeek.GetYear(date.ToDateTime(TimeOnly.MinValue));

    public static int GetIsoWeek(DateOnly date) => ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue));

    public static int ClampIsoWeek(int isoYear, int isoWeek) =>
        Math.Clamp(isoWeek, 1, ISOWeek.GetWeeksInYear(isoYear));

    // Some years only have 52 ISO weeks; clamp so callers (e.g. after a year change) never hit ISOWeek's range check.
    public static DateOnly GetMonday(int isoYear, int isoWeek) =>
        DateOnly.FromDateTime(ISOWeek.ToDateTime(isoYear, ClampIsoWeek(isoYear, isoWeek), DayOfWeek.Monday));

    public static DateOnly[] GetWeekdays(int isoYear, int isoWeek)
    {
        var monday = GetMonday(isoYear, isoWeek);
        return Enumerable.Range(0, 5).Select(monday.AddDays).ToArray();
    }

    public static string GetDiaLetra(DateOnly date) => date.DayOfWeek switch
    {
        DayOfWeek.Monday => "L",
        DayOfWeek.Tuesday => "M",
        DayOfWeek.Wednesday => "X",
        DayOfWeek.Thursday => "J",
        DayOfWeek.Friday => "V",
        DayOfWeek.Saturday => "S",
        DayOfWeek.Sunday => "D",
        _ => "?"
    };

    public static string FormatDiaCorto(DateOnly date) => $"{GetDiaLetra(date)} {date.Day}";

    public static string GetNombreMes(int mes)
    {
        var nombre = SpanishCulture.DateTimeFormat.GetMonthName(mes);
        return SpanishCulture.TextInfo.ToTitleCase(nombre);
    }
}
