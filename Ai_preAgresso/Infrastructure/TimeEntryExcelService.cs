using Ai_preAgresso.Application.Services;
using Ai_preAgresso.Domain.Models;
using ClosedXML.Excel;

namespace Ai_preAgresso.Infrastructure;

public sealed class TimeEntryExcelService
{
    private static readonly string[] Headers =
    {
        "Fecha", "Año", "Semana", "Día", "Proyecto", "Actividad", "Descripción", "Horas", "Tipo"
    };

    public void Export(IEnumerable<TimeEntry> entries, string filePath)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Agresso");

        for (var i = 0; i < Headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = Headers[i];
        }
        sheet.Row(1).Style.Font.Bold = true;

        var row = 2;
        foreach (var entry in entries.OrderBy(entry => entry.Fecha))
        {
            sheet.Cell(row, 1).Value = entry.Fecha.ToDateTime(TimeOnly.MinValue);
            sheet.Cell(row, 1).Style.DateFormat.Format = "dd/MM/yyyy";
            sheet.Cell(row, 2).Value = WeekPeriodCalculator.GetIsoYear(entry.Fecha);
            sheet.Cell(row, 3).Value = WeekPeriodCalculator.GetIsoWeek(entry.Fecha);
            sheet.Cell(row, 4).Value = WeekPeriodCalculator.FormatDiaCorto(entry.Fecha);
            sheet.Cell(row, 5).Value = entry.Proyecto;
            sheet.Cell(row, 6).Value = entry.Actividad;
            sheet.Cell(row, 7).Value = entry.Descripcion;
            sheet.Cell(row, 8).Value = entry.Horas;
            sheet.Cell(row, 9).Value = entry.Tipo.ToString();

            var fillColor = GetFillColor(entry.Tipo);
            if (fillColor.HasValue)
            {
                sheet.Range(row, 1, row, 9).Style.Fill.BackgroundColor = XLColor.FromColor(fillColor.Value);
            }

            row++;
        }

        if (row > 2)
        {
            sheet.Cell(row, 7).Value = "Total";
            sheet.Cell(row, 7).Style.Font.Bold = true;
            sheet.Cell(row, 8).FormulaA1 = $"=SUM(H2:H{row - 1})";
            sheet.Cell(row, 8).Style.Font.Bold = true;
        }

        sheet.Columns().AdjustToContents();
        workbook.SaveAs(filePath);
    }

    public List<TimeEntry> Import(string filePath)
    {
        var result = new List<TimeEntry>();
        using var workbook = new XLWorkbook(filePath);
        var sheet = workbook.Worksheets.First();

        var columnIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in sheet.Row(1).CellsUsed())
        {
            columnIndex[cell.GetString().Trim()] = cell.Address.ColumnNumber;
        }

        if (!columnIndex.TryGetValue("Fecha", out var fechaCol))
        {
            return result;
        }

        foreach (var row in sheet.RowsUsed().Skip(1))
        {
            var fechaCell = row.Cell(fechaCol);
            if (fechaCell.IsEmpty())
            {
                continue;
            }

            DateOnly fecha;
            if (fechaCell.TryGetValue(out DateTime dateTimeValue))
            {
                fecha = DateOnly.FromDateTime(dateTimeValue);
            }
            else if (DateOnly.TryParse(fechaCell.GetString(), out var parsedDate))
            {
                fecha = parsedDate;
            }
            else
            {
                continue;
            }

            var entry = new TimeEntry { Fecha = fecha };

            if (columnIndex.TryGetValue("Proyecto", out var proyectoCol))
            {
                entry.Proyecto = row.Cell(proyectoCol).GetString();
            }
            if (columnIndex.TryGetValue("Actividad", out var actividadCol))
            {
                entry.Actividad = row.Cell(actividadCol).GetString();
            }
            if (columnIndex.TryGetValue("Descripción", out var descripcionCol))
            {
                entry.Descripcion = row.Cell(descripcionCol).GetString();
            }
            if (columnIndex.TryGetValue("Horas", out var horasCol) && row.Cell(horasCol).TryGetValue(out double horas))
            {
                entry.Horas = horas;
            }
            if (columnIndex.TryGetValue("Tipo", out var tipoCol) && Enum.TryParse<TipoJornada>(row.Cell(tipoCol).GetString(), true, out var tipo))
            {
                entry.Tipo = tipo;
            }

            result.Add(entry);
        }

        return result;
    }

    private static Color? GetFillColor(TipoJornada tipo) => tipo switch
    {
        TipoJornada.Fiesta => Color.FromArgb(255, 224, 178),
        TipoJornada.Vacaciones => Color.FromArgb(179, 229, 252),
        TipoJornada.Baja => Color.FromArgb(255, 205, 210),
        _ => null
    };
}
