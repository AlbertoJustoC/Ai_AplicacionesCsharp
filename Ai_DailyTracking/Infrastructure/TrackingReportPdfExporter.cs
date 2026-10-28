using System.Drawing.Printing;
using Ai_DailyTracking.Domain.Models;
using Ai_DailyTracking.Shared.Helpers;

namespace Ai_DailyTracking.Infrastructure;

// Prints the "Crear informe" filtered table (all ficha fields except the activity and notes) plus its charts, in A3 landscape, via the Windows print pipeline
// (no third-party PDF library; the user picks "Microsoft Print to PDF" or any installed printer in the dialog).
// Each entry's row has a light gray background; below it come its activity ("Actividad: ...") and its notes, indented under a vertical line,
// one note per line, keeping the ficha colors (red timestamp, colored status).
// Every chart goes on its own final page, with a real-text header: virtual PDF printers (e.g. PDFCreator/Ghostscript) pick each page's
// orientation from its text, so an image-only page would otherwise come out portrait with the chart rotated.
public sealed class TrackingReportPdfExporter
{
    private const int RowHeight = 20;
    private const int IdColumnWidth = 50;
    private const int NotesBlockGap = 4;
    private const float NoteContinuationIndent = 14F;
    // Charts are never printed larger than this factor of their natural size (bitmap pixels at the bitmap's own dpi), so small ones don't look blown up.
    private const float MaxChartEnlargement = 1.6F;
    private const string NotesFieldKey = "notes";
    private const string ActivityFieldKey = "activity";
    private static readonly string[] WideColumnKeys = ["area", "package"];
    private static readonly Color RowBackgroundColor = Color.FromArgb(240, 240, 240);

    private IReadOnlyList<TrackingFieldDefinition> _columns = [];
    // Columns taken out of the table (the activity); their width is given to the wide columns (Area/Paquete).
    private int _removedColumnCount;
    private TrackingFieldDefinition? _statusField;
    private IReadOnlyList<TrackingEntry> _entries = [];
    private IReadOnlyList<ReportChartPage> _chartPages = [];
    private string _projectName = string.Empty;
    private int _nextRowIndex;
    private int _nextChartPageIndex;
    private bool _titlePrinted;
    // Activity + notes lines of the entry being printed, kept across pages only when they don't fit in the remaining page space.
    private List<List<NotePiece>>? _pendingDetailLines;
    private int _nextDetailLineIndex;

    private readonly record struct NotePiece(string Text, bool IsBold, Color Color, float X);

    private readonly record struct StyledText(string Text, bool IsBold, Color Color);

    public bool TryExport(IWin32Window owner, string projectName, TrackingFormSchema schema, IReadOnlyList<TrackingEntry> entries, IReadOnlyList<ReportChartPage> chartPages)
    {
        _projectName = projectName;
        _columns = schema.Fields
            .Where(field => !string.Equals(field.Key, NotesFieldKey, StringComparison.OrdinalIgnoreCase) && !string.Equals(field.Key, ActivityFieldKey, StringComparison.OrdinalIgnoreCase))
            .ToList();
        _removedColumnCount = schema.Fields.Count(field => string.Equals(field.Key, ActivityFieldKey, StringComparison.OrdinalIgnoreCase));
        _statusField = schema.Fields.FirstOrDefault(field => string.Equals(field.Key, "status", StringComparison.OrdinalIgnoreCase));
        _entries = entries;
        _chartPages = chartPages;
        _nextRowIndex = 0;
        _nextChartPageIndex = 0;
        _titlePrinted = false;
        _pendingDetailLines = null;
        _nextDetailLineIndex = 0;

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

        if (_nextChartPageIndex >= _chartPages.Count)
        {
            e.HasMorePages = false;
            return;
        }

        PrintChartPage(e, _chartPages[_nextChartPageIndex]);
        _nextChartPageIndex++;
        e.HasMorePages = _nextChartPageIndex < _chartPages.Count;
    }

    private void PrintEntriesPage(PrintPageEventArgs e)
    {
        var graphics = e.Graphics!;
        var bounds = e.MarginBounds;
        using var titleFont = new Font("Segoe UI", 16F, FontStyle.Bold);
        using var subtitleFont = new Font("Segoe UI", 10F, FontStyle.Regular);
        using var headerFont = new Font("Segoe UI", 10F, FontStyle.Bold);
        using var rowFont = new Font("Segoe UI", 9.5F, FontStyle.Regular);
        using var noteFont = new Font("Segoe UI", 9F, FontStyle.Regular);
        using var noteBoldFont = new Font("Segoe UI", 9F, FontStyle.Bold);
        using var cellFormat = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        using var noteFormat = new StringFormat(StringFormat.GenericTypographic) { FormatFlags = StringFormatFlags.NoWrap };

        var y = (float)bounds.Top;

        if (!_titlePrinted)
        {
            graphics.DrawString($"Informe de seguimiento - {_projectName}", titleFont, Brushes.Black, bounds.Left, y);
            y += 32;
            graphics.DrawString($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm} - {_entries.Count} registro(s)", subtitleFont, Brushes.Gray, bounds.Left, y);
            y += 26;
            _titlePrinted = true;
        }

        var columnWidths = GetColumnWidths(bounds.Width);

        DrawRow(graphics, headerFont, cellFormat, "ID", _columns.Select(field => (field, Text: field.Label)), bounds, y, columnWidths, isHeaderRow: true);
        y += RowHeight;
        graphics.DrawLine(Pens.Gray, bounds.Left, y - 3, bounds.Right, y - 3);

        var detailLeft = bounds.Left + IdColumnWidth;
        var detailLineHeight = noteFont.GetHeight(graphics) + 2;
        var isFirstBlockOnPage = true;

        while (_nextRowIndex < _entries.Count)
        {
            var entry = _entries[_nextRowIndex];

            if (_pendingDetailLines is null)
            {
                var detailLines = LayoutDetailLines(graphics, entry, noteFont, noteBoldFont, noteFormat, bounds.Right - detailLeft);
                var blockHeight = RowHeight + (detailLines.Count * detailLineHeight) + NotesBlockGap;

                // A row with its activity and notes is kept together on one page unless it is taller than a whole page.
                if (y + RowHeight > bounds.Bottom || (!isFirstBlockOnPage && y + blockHeight > bounds.Bottom))
                {
                    break;
                }

                DrawRow(graphics, rowFont, cellFormat, entry.EntryNumber.ToString(), _columns.Select(field => (field, Text: GetFormattedFieldValue(entry, field))), bounds, y, columnWidths, isHeaderRow: false);
                y += RowHeight;
                _pendingDetailLines = detailLines;
                _nextDetailLineIndex = 0;
            }

            var detailTop = y;

            while (_nextDetailLineIndex < _pendingDetailLines.Count && y + detailLineHeight <= bounds.Bottom)
            {
                DrawNoteLine(graphics, _pendingDetailLines[_nextDetailLineIndex], noteFont, noteBoldFont, noteFormat, detailLeft, y);
                y += detailLineHeight;
                _nextDetailLineIndex++;
            }

            if (y > detailTop)
            {
                graphics.DrawLine(Pens.Silver, detailLeft - 8, detailTop, detailLeft - 8, y);
            }

            if (_nextDetailLineIndex < _pendingDetailLines.Count)
            {
                break;
            }

            y += NotesBlockGap;
            _pendingDetailLines = null;
            _nextRowIndex++;
            isFirstBlockOnPage = false;
        }

        e.HasMorePages = _nextRowIndex < _entries.Count || _chartPages.Count > 0;
    }

    // Every column keeps the width of an even split over the original columns; the width freed by the removed activity column
    // is shared equally between the wide columns (Area/Paquete), or between all columns when none of them exists.
    private float[] GetColumnWidths(int totalWidth)
    {
        var contentWidth = totalWidth - IdColumnWidth;
        var widths = new float[_columns.Count];
        var wideIndexes = Enumerable.Range(0, _columns.Count)
            .Where(index => WideColumnKeys.Contains(_columns[index].Key, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (wideIndexes.Count == 0)
        {
            Array.Fill(widths, contentWidth / (float)Math.Max(1, _columns.Count));
            return widths;
        }

        var baseWidth = contentWidth / (float)(_columns.Count + _removedColumnCount);
        Array.Fill(widths, baseWidth);
        var freedWidth = contentWidth - (baseWidth * _columns.Count);

        foreach (var index in wideIndexes)
        {
            widths[index] += freedWidth / wideIndexes.Count;
        }

        return widths;
    }

    // Lays out the "Actividad:" line (always present) followed by every note of the entry (one per line), word-wrapped,
    // as colored word pieces relative to the detail block's left edge.
    private List<List<NotePiece>> LayoutDetailLines(Graphics graphics, TrackingEntry entry, Font regularFont, Font boldFont, StringFormat format, float maxWidth)
    {
        var lines = new List<List<NotePiece>>();
        var regularSpaceWidth = MeasureSpaceWidth(graphics, regularFont, format);
        var boldSpaceWidth = MeasureSpaceWidth(graphics, boldFont, format);

        entry.Values.TryGetValue(ActivityFieldKey, out var activityText);
        var activityLines = NoteLineParser.SplitLines(activityText);

        AppendWrappedLine(lines, BuildActivityLineSegments(activityLines.FirstOrDefault()), 0F);

        // Extra lines of a multi-line activity are indented like the wrapped continuation of a note.
        foreach (var activityLine in activityLines.Skip(1))
        {
            AppendWrappedLine(lines, [new StyledText(activityLine, false, Color.Black)], NoteContinuationIndent);
        }

        entry.Values.TryGetValue(NotesFieldKey, out var notesText);

        foreach (var noteText in NoteLineParser.SplitLines(notesText))
        {
            var segments = NoteLineParser.Parse(noteText)
                .Select(segment => new StyledText(segment.Text, segment.Kind == NoteSegmentKind.Status, GetNoteSegmentColor(segment)));
            AppendWrappedLine(lines, segments, 0F);
        }

        return lines;

        static IEnumerable<StyledText> BuildActivityLineSegments(string? firstActivityLine)
        {
            yield return new StyledText("Actividad:", true, Color.Black);

            if (!string.IsNullOrEmpty(firstActivityLine))
            {
                yield return new StyledText(" " + firstActivityLine, false, Color.Black);
            }
        }

        void AppendWrappedLine(List<List<NotePiece>> target, IEnumerable<StyledText> segments, float firstLineIndent)
        {
            var currentLine = new List<NotePiece>();
            var lineStart = firstLineIndent;
            var x = lineStart;
            var pendingSpace = false;

            foreach (var segment in segments)
            {
                var font = segment.IsBold ? boldFont : regularFont;
                var spaceWidth = segment.IsBold ? boldSpaceWidth : regularSpaceWidth;
                var words = segment.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

                if (segment.Text.Length > 0 && char.IsWhiteSpace(segment.Text[0]))
                {
                    pendingSpace = true;
                }

                for (var i = 0; i < words.Length; i++)
                {
                    pendingSpace |= i > 0;
                    var wordWidth = graphics.MeasureString(words[i], font, PointF.Empty, format).Width;
                    var gap = pendingSpace && x > lineStart ? spaceWidth : 0F;

                    if (x > lineStart && x + gap + wordWidth > maxWidth)
                    {
                        target.Add(currentLine);
                        currentLine = [];
                        lineStart = NoteContinuationIndent;
                        x = lineStart;
                        gap = 0F;
                    }

                    x += gap;
                    currentLine.Add(new NotePiece(words[i], segment.IsBold, segment.Color, x));
                    x += wordWidth;
                    pendingSpace = false;
                }

                if (segment.Text.Length > 0 && char.IsWhiteSpace(segment.Text[^1]))
                {
                    pendingSpace = true;
                }
            }

            if (currentLine.Count > 0)
            {
                target.Add(currentLine);
            }
        }
    }

    private static float MeasureSpaceWidth(Graphics graphics, Font font, StringFormat format)
    {
        return graphics.MeasureString("a a", font, PointF.Empty, format).Width - graphics.MeasureString("aa", font, PointF.Empty, format).Width;
    }

    // Same coloring as the ficha notes editor: red timestamp, status in its option color (only when it is a known option).
    private Color GetNoteSegmentColor(NoteSegment segment)
    {
        if (segment.Kind == NoteSegmentKind.Timestamp)
        {
            return Color.Red;
        }

        if (segment.Kind == NoteSegmentKind.Status && _statusField is not null)
        {
            var knownStatus = _statusField.Options.FirstOrDefault(option => string.Equals(option, segment.Text, StringComparison.OrdinalIgnoreCase));

            if (knownStatus is not null)
            {
                return OptionColorHelper.GetColor(_statusField, knownStatus);
            }
        }

        return Color.Black;
    }

    private static void DrawNoteLine(Graphics graphics, List<NotePiece> line, Font regularFont, Font boldFont, StringFormat format, float left, float y)
    {
        foreach (var piece in line)
        {
            using var brush = new SolidBrush(piece.Color);
            graphics.DrawString(piece.Text, piece.IsBold ? boldFont : regularFont, brush, left + piece.X, y, format);
        }
    }

    private void DrawRow(Graphics graphics, Font font, StringFormat cellFormat, string idCellText, IEnumerable<(TrackingFieldDefinition Field, string Text)> fieldCells, Rectangle bounds, float y, float[] columnWidths, bool isHeaderRow)
    {
        if (!isHeaderRow)
        {
            using var backgroundBrush = new SolidBrush(RowBackgroundColor);
            graphics.FillRectangle(backgroundBrush, bounds.Left, y, bounds.Width, RowHeight);
        }

        graphics.DrawString(idCellText, font, Brushes.Black, bounds.Left, y);

        var x = (float)(bounds.Left + IdColumnWidth);
        var columnIndex = 0;

        foreach (var (field, text) in fieldCells)
        {
            var columnWidth = columnWidths[columnIndex++];

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

    private void PrintChartPage(PrintPageEventArgs e, ReportChartPage chartPage)
    {
        var graphics = e.Graphics!;
        var bounds = e.MarginBounds;
        var chartBitmap = chartPage.Image;
        using var titleFont = new Font("Segoe UI", 16F, FontStyle.Bold);
        using var subtitleFont = new Font("Segoe UI", 10F, FontStyle.Regular);

        var y = (float)bounds.Top;
        graphics.DrawString($"Informe de seguimiento - {_projectName}", titleFont, Brushes.Black, bounds.Left, y);
        y += 32;
        graphics.DrawString($"{chartPage.Title} - Generado: {DateTime.Now:dd/MM/yyyy HH:mm}", subtitleFont, Brushes.Gray, bounds.Left, y);
        y += 30;

        // Fit the whole chart (it can include its own title) below the header while keeping its width/height proportion.
        var availableHeight = bounds.Bottom - y;
        var naturalUnitsPerPixel = 100F / chartBitmap.HorizontalResolution;
        var scale = Math.Min(Math.Min(bounds.Width / (float)chartBitmap.Width, availableHeight / chartBitmap.Height), naturalUnitsPerPixel * MaxChartEnlargement);
        var drawWidth = chartBitmap.Width * scale;
        var drawHeight = chartBitmap.Height * scale;
        var drawX = bounds.Left + ((bounds.Width - drawWidth) / 2F);

        graphics.DrawImage(chartBitmap, drawX, y, drawWidth, drawHeight);
    }
}

// One chart image printed on its own page, with the title shown in the page header.
public sealed record ReportChartPage(string Title, Bitmap Image);

