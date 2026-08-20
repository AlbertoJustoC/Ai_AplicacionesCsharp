using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Ai_preAgresso.UI.Controls;

public sealed class PieChartPanel : Panel
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

    private IReadOnlyList<(string Label, double Value)> _slices = [];
    private string _title = string.Empty;

    public PieChartPanel()
    {
        DoubleBuffered = true;
        BackColor = Color.White;
    }

    public void SetData(string title, IReadOnlyList<(string Label, double Value)> slices)
    {
        _title = title;
        _slices = slices;
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
        using var emptyFont = new Font("Segoe UI", 10F, FontStyle.Italic);
        using var titleBrush = new SolidBrush(Color.FromArgb(34, 44, 58));
        using var labelBrush = new SolidBrush(Color.FromArgb(78, 86, 99));

        graphics.DrawString(_title, titleFont, titleBrush, new PointF(12, 10));

        const int chartTop = 46;
        const int legendWidth = 240;
        var chartAreaWidth = Width - legendWidth - 32;
        var chartAreaHeight = Height - chartTop - 16;
        var total = _slices.Sum(slice => slice.Value);

        if (_slices.Count == 0 || total <= 0 || chartAreaWidth <= 40 || chartAreaHeight <= 40)
        {
            graphics.DrawString("Sin datos para los filtros seleccionados.", emptyFont, labelBrush, new PointF(16, chartTop + 20));
            return;
        }

        var diameter = Math.Min(chartAreaWidth, chartAreaHeight);
        var pieRect = new RectangleF(16, chartTop, diameter, diameter);

        using var borderPen = new Pen(Color.White, 1.5F);
        var startAngle = -90F;
        for (var index = 0; index < _slices.Count; index++)
        {
            var sweep = (float)(_slices[index].Value / total * 360.0);
            using var brush = new SolidBrush(Palette[index % Palette.Length]);
            graphics.FillPie(brush, pieRect.X, pieRect.Y, pieRect.Width, pieRect.Height, startAngle, sweep);
            graphics.DrawPie(borderPen, pieRect.X, pieRect.Y, pieRect.Width, pieRect.Height, startAngle, sweep);
            startAngle += sweep;
        }

        var legendX = pieRect.Right + 24;
        var legendY = chartTop;
        for (var index = 0; index < _slices.Count; index++)
        {
            var (label, value) = _slices[index];
            using var swatchBrush = new SolidBrush(Palette[index % Palette.Length]);
            graphics.FillRectangle(swatchBrush, legendX, legendY + 3, 12, 12);

            var percent = value / total * 100.0;
            using var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter };
            var textRect = new RectangleF(legendX + 18, legendY, legendWidth - 18, 36);
            graphics.DrawString($"{label}: {value:0.##} h ({percent:0.#}%)", labelFont, labelBrush, textRect, format);
            legendY += 36;
        }
    }
}
