using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Text;
using Ai_DailyTracking.Application.Services;
using Ai_DailyTracking.Domain.Models;
using Ai_DailyTracking.Shared.Helpers;

namespace Ai_DailyTracking.UI.Controls;

public sealed class TrackingTimelinePanel : Panel
{
    private const int PlotLeft = 112;
    private const int PlotTop = 100;
    private const int RowHeight = 38;
    private const float NaturalDayWidth = 78F;
    // Narrowest day column used when printing; below this the weekly date labels would overlap.
    private const float MinPrintDayWidth = 10F;
    private IReadOnlyList<TrackingTimelineRow> _rows = [];
    private readonly List<RowLayout> _layouts = [];
    private TrackingFieldDefinition? _statusField;
    private DateTime _axisStart;
    private DateTime _axisEnd;
    private float _dayWidth = NaturalDayWidth;
    private string _message = "Sin datos para los filtros seleccionados.";
    private readonly ToolTip _toolTip = new() { AutoPopDelay = 30000, InitialDelay = 250, ReshowDelay = 100 };
    private string? _hoverKey;

    // The 7 px inset (of a 78 px day) keeps the same proportion when the day column is narrowed for printing.
    private float DayInset => _dayWidth * 7F / NaturalDayWidth;

    private float ActiveWidth => _dayWidth - (2 * DayInset);

    public TrackingTimelinePanel()
    {
        DoubleBuffered = true;
        AutoScroll = true;
        BackColor = Color.White;
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    public void SetData(IReadOnlyList<TrackingTimelineRow> rows, TrackingFieldDefinition? statusField, string? message = null)
    {
        _rows = rows;
        _statusField = statusField;
        _message = message ?? "Sin datos para los filtros seleccionados.";
        _toolTip.Hide(this);
        _hoverKey = null;

        ApplyLayout(NaturalDayWidth);
        Invalidate();
    }

    private void ApplyLayout(float dayWidth)
    {
        _dayWidth = dayWidth;

        if (_rows.Count > 0)
        {
            var firstDate = _rows.SelectMany(row => row.Notes.Select(note => note.Timestamp.Date).Append(row.Start.Date)).Min();
            _axisStart = firstDate.AddDays(-((int)firstDate.DayOfWeek + 6) % 7);
            _axisEnd = _rows.SelectMany(row => row.Notes.Select(note => note.Timestamp.Date).Append(row.End.Date)).Max();
            _layouts.Clear();
            var top = PlotTop;
            foreach (var row in _rows)
            {
                var markers = BuildMarkers(row);
                var laneCount = markers.Count == 0 ? 1 : markers.Max(marker => marker.Lane) + 1;
                var height = Math.Max(RowHeight, 18 + laneCount * 15);
                _layouts.Add(new RowLayout(top, height, markers));
                top += height;
            }
            var days = (_axisEnd - _axisStart).Days + 1;
            AutoScrollMinSize = new Size((int)Math.Ceiling(PlotLeft + days * _dayWidth + 30), top + 44);
        }
        else
        {
            _layouts.Clear();
            AutoScrollMinSize = Size.Empty;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);
        PaintContent(e.Graphics);
    }

    // Renders the whole timeline (not just the visible scrolled area) to a bitmap meant to be printed on a fitWidth x fitHeight area (at 96 dpi).
    // Instead of just shrinking it, the day columns are narrowed first so a long date range stays readable; it is only scaled down
    // when the rows alone do not fit. The bitmap has pixelDensity times more pixels than that area for sharper printing.
    public Bitmap? RenderToBitmap(int fitWidth, int fitHeight, float pixelDensity)
    {
        if (_rows.Count == 0)
        {
            return null;
        }

        var days = (_axisEnd - _axisStart).Days + 1;
        var scale = 1F;

        // The row heights depend on the day width (note markers overlap on narrow days), so settle both over a few passes.
        for (var pass = 0; pass < 3; pass++)
        {
            ApplyLayout(Math.Clamp(((fitWidth / scale) - PlotLeft - 30) / days, MinPrintDayWidth, NaturalDayWidth));
            scale = Math.Min(1F, fitHeight / (float)AutoScrollMinSize.Height);
        }

        var content = AutoScrollMinSize;
        scale = Math.Min(scale, fitWidth / (float)content.Width);
        var drawScale = scale * pixelDensity;
        var bitmap = new Bitmap(Math.Max(1, (int)Math.Ceiling(content.Width * drawScale)), Math.Max(1, (int)Math.Ceiling(content.Height * drawScale)));

        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(BackColor);
            graphics.ScaleTransform(drawScale, drawScale);
            PaintContent(graphics);
        }

        // Set after drawing: the graphics would otherwise scale the font sizes by this dpi as well.
        bitmap.SetResolution(96F * pixelDensity, 96F * pixelDensity);
        ApplyLayout(NaturalDayWidth);
        return bitmap;
    }

    private void PaintContent(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        using var titleFont = new Font("Segoe UI Semibold", 12F, FontStyle.Bold);
        using var labelFont = new Font("Segoe UI", 8.5F);
        using var boldFont = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        using var titleBrush = new SolidBrush(Color.FromArgb(34, 44, 58));
        using var labelBrush = new SolidBrush(Color.FromArgb(78, 86, 99));
        using var gridPen = new Pen(Color.FromArgb(226, 230, 235));

        g.DrawString("Línea de tiempo por registro", titleFont, titleBrush, 12, 10);
        if (_rows.Count == 0)
        {
            g.DrawString(_message, labelFont, labelBrush, 16, 49);
            return;
        }

        g.DrawString("Cada día: 08:00–17:00  ·  Los puntos muestran las notas", labelFont, labelBrush, 14, 40);
        g.DrawString("ID", boldFont, labelBrush, 22, PlotTop - 28);

        var bottom = _layouts[^1].Top + _layouts[^1].Height;
        for (var index = 0; index < _layouts.Count; index += 2)
        {
            using var stripe = new SolidBrush(Color.FromArgb(247, 249, 251));
            g.FillRectangle(stripe, 0, _layouts[index].Top, AutoScrollMinSize.Width, _layouts[index].Height);
        }

        for (var monday = _axisStart; monday <= _axisEnd; monday = monday.AddDays(7))
        {
            var x = Map(monday.AddHours(8));
            g.DrawLine(gridPen, x, PlotTop - 5, x, bottom);
            g.DrawString(monday.ToString("dd/MM/yyyy"), labelFont, labelBrush, x - 29, PlotTop - 35);
        }

        for (var index = 0; index < _rows.Count; index++)
        {
            var row = _rows[index];
            var layout = _layouts[index];
            var y = layout.Top + layout.Height / 2F;

            g.DrawString(row.EntryNumber.ToString(), boldFont, titleBrush, 23, y - 9);
            var color = _statusField is null ? Color.Gainsboro : OptionColorHelper.GetColor(_statusField, row.Status);
            using var linePen = new Pen(color, 5F) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using var connectorPen = new Pen(color, 1F);
            using var pointBrush = new SolidBrush(color);
            using var outlinePen = new Pen(Color.White, 1.5F);
            var startX = Map(row.Start);
            var endX = Map(row.End);
            g.DrawLine(linePen, Math.Min(startX, endX), y, Math.Max(startX, endX), y);

            var laneCount = layout.Markers.Count == 0 ? 1 : layout.Markers.Max(marker => marker.Lane) + 1;
            foreach (var marker in layout.Markers)
            {
                var pointY = y + (marker.Lane - (laneCount - 1) / 2F) * 15;
                if (pointY != y)
                    g.DrawLine(connectorPen, marker.X, y, marker.X, pointY);
                g.FillEllipse(pointBrush, marker.X - 6, pointY - 6, 12, 12);
                g.DrawEllipse(outlinePen, marker.X - 6, pointY - 6, 12, 12);
                if (marker.Notes.Count > 1)
                {
                    using var countFont = new Font("Segoe UI", 7F, FontStyle.Bold);
                    g.DrawString(marker.Notes.Count.ToString(), countFont, titleBrush, marker.X + 6, pointY - 15);
                }
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var x = e.X - AutoScrollPosition.X;
        var y = e.Y - AutoScrollPosition.Y;
        var index = _layouts.FindIndex(layout => y >= layout.Top && y < layout.Top + layout.Height);
        if (index < 0 || index >= _rows.Count)
        {
            ClearHover();
            return;
        }

        var row = _rows[index];
        var layout = _layouts[index];
        var rowY = layout.Top + layout.Height / 2F;
        var laneCount = layout.Markers.Count == 0 ? 1 : layout.Markers.Max(marker => marker.Lane) + 1;
        for (var pointIndex = 0; pointIndex < layout.Markers.Count; pointIndex++)
        {
            var marker = layout.Markers[pointIndex];
            var pointY = rowY + (marker.Lane - (laneCount - 1) / 2F) * 15;
            if (Math.Abs(x - marker.X) <= 7 && Math.Abs(y - pointY) <= 7)
            {
                var noteText = new StringBuilder();
                foreach (var note in marker.Notes)
                {
                    if (noteText.Length > 0) noteText.AppendLine().AppendLine();
                    noteText.Append(note.Timestamp.ToString("dd/MM/yyyy HH:mm"))
                        .Append(" — ").Append(note.Text);
                }
                ShowHover($"{index}:note:{pointIndex}", noteText.ToString(), e.Location);
                return;
            }
        }

        if (Math.Abs(y - rowY) <= 7 && x >= Map(row.Start) - 5 && x <= Map(row.End) + 5)
        {
            ShowHover($"{index}:line", row.Summary, e.Location);
            return;
        }

        ClearHover();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        ClearHover();
    }

    private void ShowHover(string key, string text, Point location)
    {
        if (_hoverKey == key) return;
        _toolTip.Hide(this);
        _hoverKey = key;
        _toolTip.Show(text, this, location.X + 14, location.Y + 16, 30000);
    }

    private void ClearHover()
    {
        if (_hoverKey is null) return;
        _toolTip.Hide(this);
        _hoverKey = null;
    }

    private float Map(DateTime date) =>
        PlotLeft + (float)(date.Date - _axisStart).TotalDays * _dayWidth + DayInset
        + (float)TrackingTimelineBuilder.DayFraction(date) * ActiveWidth;

    private List<NoteMarker> BuildMarkers(TrackingTimelineRow row)
    {
        var markers = row.Notes
            .GroupBy(note => (note.Timestamp.Date, Fraction: TrackingTimelineBuilder.DayFraction(note.Timestamp)))
            .Select(group => new NoteMarker(Map(group.First().Timestamp), group.ToList()))
            .OrderBy(marker => marker.X).ToList();
        var laneEnds = new List<float>();
        foreach (var marker in markers)
        {
            var lane = laneEnds.FindIndex(lastX => marker.X - lastX >= 15);
            if (lane < 0)
            {
                lane = laneEnds.Count;
                laneEnds.Add(marker.X);
            }
            else laneEnds[lane] = marker.X;
            marker.Lane = lane;
        }
        return markers;
    }

    private sealed record RowLayout(int Top, int Height, List<NoteMarker> Markers);

    private sealed class NoteMarker(float x, List<TrackingTimelineNote> notes)
    {
        public float X { get; } = x;
        public List<TrackingTimelineNote> Notes { get; } = notes;
        public int Lane { get; set; }
    }
}
