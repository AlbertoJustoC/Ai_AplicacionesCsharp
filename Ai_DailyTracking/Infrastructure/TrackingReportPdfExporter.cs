using System.Drawing.Printing;
using Ai_DailyTracking.Domain.Models;
using Ai_DailyTracking.Shared.Helpers;

namespace Ai_DailyTracking.Infrastructure;

// Prints the "Crear informe" filtered table (all ficha fields) plus its chart, in A3 landscape, via the Windows print pipeline
// (no third-party PDF library; the user picks "Microsoft Print to PDF" or any installed printer in the dialog).
public sealed class TrackingReportPdfExporter
{
    private const int RowHeight = 20;
    private const int IdColumnWidth = 50;

    private TrackingFormSchema _schema = null!;
    private TrackingFieldDefinition? _statusField;
    private IReadOnlyList<TrackingEntry> _entries = [];
    private Bitmap? _chartBitmap;
    private string _projectName = string.Empty;
    private int _nextRowIndex;

    public bool TryExport(IWin32Window owner, string projectName, TrackingFormSchema schema, IReadOnlyList<TrackingEntry> entries, Bitmap? chartBitmap)
    {
        _projectName = projectName;
        _schema = schema;
        _statusField = schema.Fields.FirstOrDefault(field => string.Equals(field.Key, "status", StringComparison.OrdinalIgnoreCase));
        _entries = entries;
        _chartBitmap = chartBitmap;
        _nextRowIndex = 0;

        using var printDocument = new PrintDocument();
        printDocument.DocumentName = $"Informe de seguimiento - {projectName}";
        printDocument.DefaultPageSettings.Landscape = true;
        printDocument.DefaultPageSettings.PaperSize = GetA3PaperSize(printDocument.PrinterSettings);
        printDocument.PrintPage += PrintDocument_PrintPage;

        using var printDialog = new PrintDialog { Document = printDocument, AllowSomePages = false, AllowSelection = false };

        if (printDialog.ShowDialog(owner) != DialogResult.OK)
        {
            return false;
        }

        printDocument.Print();
        return true;
    }

    private static PaperSize GetA3PaperSize(PrinterSettings printerSettings)
    {
        foreach (PaperSize paperSize in printerSettings.PaperSizes)
        {
            if (paperSize.Kind == PaperKind.A3)
            {
                return paperSize;
            }
        }

        // Fallback for printers that don't report A3 in their capabilities (e.g. some PDF drivers): 297 x 420 mm in hundredths of an inch.
        return new PaperSize("A3", 1169, 1654);
    }

    private void PrintDocument_PrintPage(object? sender, PrintPageEventArgs e)
    {
        if (_nextRowIndex < _entries.Count)
        {
            PrintEntriesPage(e);
            return;
        }

        if (_chartBitmap is null)
        {
            e.HasMorePages = false;
            return;
        }

        PrintChartPage(e);
        e.HasMorePages = false;
    }

    private void PrintEntriesPage(PrintPageEventArgs e)
    {
        var graphics = e.Graphics!;
        var bounds = e.MarginBounds;
        using var titleFont = new Font("Segoe UI", 16F, FontStyle.Bold);
        using var subtitleFont = new Font("Segoe UI", 10F, FontStyle.Regular);
        using var headerFont = new Font("Segoe UI", 10F, FontStyle.Bold);
        using var rowFont = new Font("Segoe UI", 9.5F, FontStyle.Regular);
        using var cellFormat = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };

        var y = bounds.Top;

        if (_nextRowIndex == 0)
        {
            graphics.DrawString($"Informe de seguimiento - {_projectName}", titleFont, Brushes.Black, bounds.Left, y);
            y += 32;
            graphics.DrawString($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm} - {_entries.Count} registro(s)", subtitleFont, Brushes.Gray, bounds.Left, y);
            y += 26;
        }

        var columnWidth = (bounds.Width - IdColumnWidth) / (float)Math.Max(1, _schema.Fields.Count);

        DrawRow(graphics, headerFont, cellFormat, "ID", _schema.Fields.Select(field => (field, Text: field.Label)), bounds, y, columnWidth, isHeaderRow: true);
        y += RowHeight;
        graphics.DrawLine(Pens.Gray, bounds.Left, y - 3, bounds.Right, y - 3);

        while (_nextRowIndex < _entries.Count && y + RowHeight <= bounds.Bottom)
        {
            var entry = _entries[_nextRowIndex];
            DrawRow(graphics, rowFont, cellFormat, entry.EntryNumber.ToString(), _schema.Fields.Select(field => (field, Text: GetFormattedFieldValue(entry, field))), bounds, y, columnWidth, isHeaderRow: false);
            y += RowHeight;
            _nextRowIndex++;
        }

        e.HasMorePages = _nextRowIndex < _entries.Count || _chartBitmap is not null;
    }

    private void DrawRow(Graphics graphics, Font font, StringFormat cellFormat, string idCellText, IEnumerable<(TrackingFieldDefinition Field, string Text)> fieldCells, Rectangle bounds, int y, float columnWidth, bool isHeaderRow)
    {
        graphics.DrawString(idCellText, font, Brushes.Black, bounds.Left, y);

        var x = (float)(bounds.Left + IdColumnWidth);

        foreach (var (field, text) in fieldCells)
        {
            if (!isHeaderRow && _statusField is not null && string.Equals(field.Key, _statusField.Key, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(text))
            {
                var swatchRect = new RectangleF(x, y + ((RowHeight - 10) / 2F), 10, 10);
                using (var swatchBrush = new SolidBrush(OptionColorHelper.GetColor(_statusField, text)))
                {
                    graphics.FillRectangle(swatchBrush, swatchRect);
                }

                graphics.DrawRectangle(Pens.Gray, swatchRect.X, swatchRect.Y, swatchRect.Width, swatchRect.Height);
                graphics.DrawString(text, font, Brushes.Black, new RectangleF(x + 14, y, columnWidth - 20, RowHeight), cellFormat);
            }
            else
            {
                graphics.DrawString(text, font, Brushes.Black, new RectangleF(x, y, columnWidth - 6, RowHeight), cellFormat);
            }

            x += columnWidth;
        }
    }

    private static string GetFormattedFieldValue(TrackingEntry entry, TrackingFieldDefinition field)
    {
        if (!entry.Values.TryGetValue(field.Key, out var rawValue) || string.IsNullOrWhiteSpace(rawValue))
        {
            return string.Empty;
        }

        if (field.Type == TrackingFieldType.Date && DateTime.TryParse(rawValue, out var parsedDate))
        {
            return parsedDate.ToString("dd/MM/yyyy");
        }

        return rawValue;
    }

    private void PrintChartPage(PrintPageEventArgs e)
    {
        var graphics = e.Graphics!;
        var bounds = e.MarginBounds;
        var chartBitmap = _chartBitmap!;

        // Fit the whole chart (already includes its own title) to the page while keeping its width/height proportion.
        var scale = Math.Min(bounds.Width / (float)chartBitmap.Width, bounds.Height / (float)chartBitmap.Height);
        var drawWidth = chartBitmap.Width * scale;
        var drawHeight = chartBitmap.Height * scale;
        var drawX = bounds.Left + ((bounds.Width - drawWidth) / 2F);
        var drawY = bounds.Top + ((bounds.Height - drawHeight) / 2F);

        graphics.DrawImage(chartBitmap, drawX, drawY, drawWidth, drawHeight);
    }
}

