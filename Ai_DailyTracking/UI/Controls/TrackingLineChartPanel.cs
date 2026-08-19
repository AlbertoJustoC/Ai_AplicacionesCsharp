using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Ai_DailyTracking.UI.Controls;

// Line chart: X axis = distinct entry dates, Y axis = record count per day, one line per selected series.
public sealed class TrackingLineChartPanel : Panel
{
    private static readonly Color[] Palette =
    [
        Color.FromArgb(18, 103, 177),
        Color.FromArgb(232, 144, 34),
        Color.FromArgb(46, 139, 87),
        Color.FromArgb(178, 34, 52),
        Color.FromArgb(111, 66, 193),
        Color.FromArgb(23, 162, 184),
        Color.FromArgb(214, 158, 46),
        Color.FromArgb(96, 108, 122)
    ];

    private IReadOnlyList<DateTime> _dates = [];
    private IReadOnlyList<(string Label, IReadOnlyList<int> Counts)> _series = [];
    private string _title = string.Empty;

    public TrackingLineChartPanel()
    {
        DoubleBuffered = true;
        BackColor = Color.White;
        // Without this, growing the panel leaves the newly exposed area unpainted until something else invalidates it.
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    public void SetData(string title, IReadOnlyList<DateTime> dates, IReadOnlyList<(string Label, IReadOnlyList<int> Counts)> series)
    {
        _title = title;
        _dates = dates;
        _series = series;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        using var titleFont = new Font("Segoe UI Semibold", 12F, FontStyle.Bold);
        using var titleBrush = new SolidBrush(Color.FromArgb(34, 44, 58));
        graphics.DrawString(_title, titleFont, titleBrush, new PointF(12, 10));

        using var labelFont = new Font("Segoe UI", 8.5F);
        using var labelBrush = new SolidBrush(Color.FromArgb(78, 86, 99));

        if (_dates.Count == 0 || _series.Count == 0)
        {
            using var emptyFont = new Font("Segoe UI", 10F, FontStyle.Italic);
            graphics.DrawString("Sin datos para los filtros seleccionados.", emptyFont, labelBrush, new PointF(16, 46));
            return;
        }

        const int chartTop = 46;
        const int legendHeight = 24;
        var chartBottom = Height - 44;
        const int chartLeft = 44;
        var chartRight = Width - 16;

        if (chartBottom <= chartTop + legendHeight || chartRight <= chartLeft)
        {
            return;
        }

        DrawLegend(graphics, labelFont, chartLeft, chartTop);

        var plotTop = chartTop + legendHeight;
        var maxCount = Math.Max(1, _series.SelectMany(series => series.Counts).DefaultIfEmpty(0).Max());
        const int gridLineCount = 4;
        var step = Math.Max(1, (int)Math.Ceiling(maxCount / (double)gridLineCount));
        var axisTop = step * gridLineCount;

        using var axisLabelFont = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        using var gridPen = new Pen(Color.FromArgb(230, 233, 237));
        using var axisPen = new Pen(Color.FromArgb(190, 196, 204));

        DrawYAxisAndGrid(graphics, axisLabelFont, labelBrush, gridPen, axisPen, chartLeft, chartRight, plotTop, chartBottom, step, axisTop);
        DrawXAxisLabels(graphics, labelFont, labelBrush, chartLeft, chartRight, chartBottom);
        DrawSeriesLines(graphics, chartLeft, chartRight, plotTop, chartBottom, axisTop);
    }

    private void DrawLegend(Graphics graphics, Font labelFont, int chartLeft, int chartTop)
    {
        var legendX = (float)chartLeft;

        for (var index = 0; index < _series.Count; index++)
        {
            var color = Palette[index % Palette.Length];
            using var swatchBrush = new SolidBrush(color);
            graphics.FillRectangle(swatchBrush, legendX, chartTop + 4, 12, 12);

            var label = _series[index].Label;
            using var textBrush = new SolidBrush(Color.FromArgb(78, 86, 99));
            graphics.DrawString(label, labelFont, textBrush, legendX + 16, chartTop + 2);

            var labelSize = graphics.MeasureString(label, labelFont);
            legendX += 16 + labelSize.Width + 18;
        }
    }

    private static void DrawYAxisAndGrid(Graphics graphics, Font axisLabelFont, Brush labelBrush, Pen gridPen, Pen axisPen, int chartLeft, int chartRight, int plotTop, int chartBottom, int step, int axisTop)
    {
        var chartHeight = chartBottom - plotTop;

        for (var value = 0; value <= axisTop; value += step)
        {
            var y = chartBottom - (value / (float)axisTop * chartHeight);
            graphics.DrawLine(gridPen, chartLeft, y, chartRight, y);

            var text = value.ToString();
            var textSize = graphics.MeasureString(text, axisLabelFont);
            graphics.DrawString(text, axisLabelFont, labelBrush, chartLeft - textSize.Width - 6, y - (textSize.Height / 2F));
        }

        graphics.DrawLine(axisPen, chartLeft, plotTop, chartLeft, chartBottom);
        graphics.DrawLine(axisPen, chartLeft, chartBottom, chartRight, chartBottom);
    }

    private void DrawXAxisLabels(Graphics graphics, Font labelFont, Brush labelBrush, int chartLeft, int chartRight, int chartBottom)
    {
        var slotWidth = (chartRight - chartLeft) / (float)Math.Max(1, _dates.Count - 1);
        var maxLabels = Math.Max(2, (int)((chartRight - chartLeft) / 55F));
        var labelStride = Math.Max(1, (int)Math.Ceiling(_dates.Count / (double)maxLabels));

        using var format = new StringFormat { Alignment = StringAlignment.Center };

        for (var index = 0; index < _dates.Count; index += labelStride)
        {
            var x = chartLeft + (slotWidth * index);
            var text = _dates[index].ToString("dd MMM");
            graphics.DrawString(text, labelFont, labelBrush, new RectangleF(x - 30, chartBottom + 6, 60, 18), format);
        }
    }

    private void DrawSeriesLines(Graphics graphics, int chartLeft, int chartRight, int plotTop, int chartBottom, int axisTop)
    {
        var chartHeight = chartBottom - plotTop;
        var slotWidth = (chartRight - chartLeft) / (float)Math.Max(1, _dates.Count - 1);

        for (var seriesIndex = 0; seriesIndex < _series.Count; seriesIndex++)
        {
            var counts = _series[seriesIndex].Counts;
            var color = Palette[seriesIndex % Palette.Length];
            using var linePen = new Pen(color, 2F);
            using var pointBrush = new SolidBrush(color);

            PointF? previousPoint = null;

            for (var dateIndex = 0; dateIndex < _dates.Count; dateIndex++)
            {
                var count = dateIndex < counts.Count ? counts[dateIndex] : 0;
                var x = chartLeft + (slotWidth * dateIndex);
                var y = chartBottom - (count / (float)axisTop * chartHeight);
                var point = new PointF(x, y);

                if (previousPoint is not null)
                {
                    graphics.DrawLine(linePen, previousPoint.Value, point);
                }

                graphics.FillEllipse(pointBrush, x - 3, y - 3, 6, 6);
                previousPoint = point;
            }
        }
    }
}
