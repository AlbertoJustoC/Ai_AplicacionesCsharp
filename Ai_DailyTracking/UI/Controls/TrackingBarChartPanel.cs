using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Ai_DailyTracking.UI.Controls;

public sealed class TrackingBarChartPanel : Panel
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

    private IReadOnlyList<(string Label, int Count)> _bars = [];
    private string _title = string.Empty;

    public TrackingBarChartPanel()
    {
        DoubleBuffered = true;
        BackColor = Color.White;
    }

    public void SetData(string title, IReadOnlyList<(string Label, int Count)> bars)
    {
        _title = title;
        _bars = bars;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        using var titleFont = new Font("Segoe UI Semibold", 12F, FontStyle.Bold);
        using var labelFont = new Font("Segoe UI", 9F);
        using var countFont = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
        using var titleBrush = new SolidBrush(Color.FromArgb(34, 44, 58));
        using var labelBrush = new SolidBrush(Color.FromArgb(78, 86, 99));
        using var axisPen = new Pen(Color.FromArgb(220, 224, 229));

        graphics.DrawString(_title, titleFont, titleBrush, new PointF(12, 10));

        const int chartTop = 46;
        var chartBottom = Height - 48;
        const int chartLeft = 16;
        var chartRight = Width - 16;

        if (chartBottom <= chartTop || chartRight <= chartLeft)
        {
            return;
        }

        graphics.DrawLine(axisPen, chartLeft, chartBottom, chartRight, chartBottom);

        if (_bars.Count == 0)
        {
            using var emptyFont = new Font("Segoe UI", 10F, FontStyle.Italic);
            graphics.DrawString("Sin datos para los filtros seleccionados.", emptyFont, labelBrush, new PointF(chartLeft, chartTop + 20));
            return;
        }

        var maxCount = Math.Max(1, _bars.Max(bar => bar.Count));
        var chartHeight = chartBottom - chartTop;
        var barSlotWidth = (float)(chartRight - chartLeft) / _bars.Count;
        var barWidth = Math.Min(70F, barSlotWidth * 0.55F);

        for (var index = 0; index < _bars.Count; index++)
        {
            var (label, count) = _bars[index];
            var barHeight = (int)(count / (float)maxCount * (chartHeight - 24));
            var slotCenterX = chartLeft + (barSlotWidth * index) + (barSlotWidth / 2F);
            var barLeft = slotCenterX - (barWidth / 2F);
            var barTop = chartBottom - barHeight;

            using var barBrush = new SolidBrush(Palette[index % Palette.Length]);
            graphics.FillRectangle(barBrush, barLeft, barTop, barWidth, barHeight);

            var countText = count.ToString();
            var countSize = graphics.MeasureString(countText, countFont);
            graphics.DrawString(countText, countFont, titleBrush, slotCenterX - (countSize.Width / 2F), barTop - countSize.Height - 2);

            using var format = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
            var labelRect = new RectangleF(slotCenterX - (barSlotWidth / 2F), chartBottom + 6, barSlotWidth, 38);
            graphics.DrawString(label, labelFont, labelBrush, labelRect, format);
        }
    }
}
