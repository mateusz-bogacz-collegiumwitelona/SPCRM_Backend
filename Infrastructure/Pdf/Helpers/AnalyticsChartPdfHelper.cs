using Infrastructure.Pdf.Command;
using ScottPlot;
using SkiaSharp;

namespace Infrastructure.Pdf.Helpers
{
    public static class AnalyticsChartPdfHelper
    {
        public static string GenerateRevenueChartImage(List<EmployeeHistoryMetricCommand> historyMetrics)
        {
            var plot = new Plot();

            plot.FigureBackground.Color = Color.FromHex("#ffffff");
            plot.DataBackground.Color = Color.FromHex("#ffffff");

            var safeFontName = GetSystemFontName();

            var values = historyMetrics.Select(m => (double)m.Revenue).ToArray();
            var labels = historyMetrics.Select(m => m.Label).ToArray();

            var bars = new List<Bar>();
            var ticks = new ScottPlot.Tick[labels.Length];

            for (int i = 0; i < values.Length; i++)
            {
                bars.Add(new Bar
                {
                    Position = i,
                    Value = values[i],
                    FillColor = Color.FromHex("#1e40af"),
                    LineWidth = 0
                });

                ticks[i] = new ScottPlot.Tick(i, labels[i]);
            }

            plot.Add.Bars(bars);

            plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks);
            plot.Axes.Bottom.TickLabelStyle.FontName = safeFontName;
            plot.Axes.Bottom.TickLabelStyle.FontSize = 11;
            plot.Axes.Bottom.TickLabelStyle.Rotation = -35;
            plot.Axes.Bottom.TickLabelStyle.Alignment = Alignment.MiddleRight;
            plot.Axes.Bottom.TickLabelStyle.ForeColor = Color.FromHex("#111827");

            plot.Axes.Left.Label.Text = "Przychód (PLN)";
            plot.Axes.Left.Label.FontName = safeFontName;
            plot.Axes.Left.Label.FontSize = 12;
            plot.Axes.Left.Label.Bold = true;
            plot.Axes.Left.Label.ForeColor = Color.FromHex("#111827");

            plot.Axes.Left.TickLabelStyle.FontName = safeFontName;
            plot.Axes.Left.TickLabelStyle.FontSize = 10;
            plot.Axes.Left.TickLabelStyle.ForeColor = Color.FromHex("#111827");

            plot.Axes.SetLimitsX(-0.6, Math.Max(values.Length - 0.4, 1));
            var maxVal = values.Length > 0 && values.Max() > 0 ? values.Max() * 1.15 : 1000;
            plot.Axes.SetLimitsY(0, maxVal);

            plot.Layout.Fixed(new PixelPadding(90, 25, 75, 20));
            plot.Grid.MajorLineColor = Color.FromHex("#e5e7eb");

            return plot.GetSvgXml(850, 420);
        }

        private static string GetSystemFontName()
        {
            var installedFonts = SKFontManager.Default.GetFontFamilies();

            string[] preferredFonts = { "DejaVu Sans", "Liberation Sans", "Arial", "Ubuntu", "Segoe UI" };

            foreach (var font in preferredFonts)
            {
                if (installedFonts.Contains(font))
                    return font;
            }

            return SKTypeface.Default.FamilyName ?? Fonts.Sans;
        }
    }
}
