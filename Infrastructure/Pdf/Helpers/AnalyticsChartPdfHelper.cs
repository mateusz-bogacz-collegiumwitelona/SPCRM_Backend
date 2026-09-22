using Domain.Constants;
using Infrastructure.Pdf.Command;
using ScottPlot;
using SkiaSharp;

namespace Infrastructure.Pdf.Helpers
{
    public static class AnalyticsChartPdfHelper
    {
        public static List<(string CurrencyCode, string svg)> GenerateRevenueChartsPerCurrency(List<HistoryMetricCommand> historyCommand)
        {
            var result = new List<(string CurrencyCode, string svg)>();

            if (historyCommand == null || !historyCommand.Any())
                return result;

            var distinctCurrencies = historyCommand
                .SelectMany(h => h.Revenue)
                .Select(r => r.CurrencyCode)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            if (!distinctCurrencies.Any())
            {
                distinctCurrencies.Add(BusinessConstants.DefaultCurrencyCode);
            }

            var safeFontName = GetSystemFontName();
            var labels = historyCommand.Select(h => h.Label).ToArray();

            foreach (var currency in distinctCurrencies)
            {
                var plot = new Plot();
                plot.FigureBackground.Color = Color.FromHex("#ffffff");
                plot.DataBackground.Color = Color.FromHex("#ffffff");

                var values = historyCommand.Select(m =>
                {
                    var match = m.Revenue.FirstOrDefault(r => r.CurrencyCode == currency);
                    return match != null ? (double)match.Amount : 0.0;
                }).ToArray();

                var bar = new List<Bar>();
                var ticks = new Tick[labels.Length];

                for (int i = 0; i < labels.Length; i++)
                {
                    bar.Add(new Bar
                    {
                        Position = i,
                        Value = values[i],
                        FillColor = Color.FromHex("#1e40af"),
                        LineWidth = 0
                    });

                    ticks[i] = new Tick(i, labels[i]);
                }

                plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks);
                plot.Add.Bars(bar);
                plot.Axes.Bottom.TickLabelStyle.FontName = safeFontName;
                plot.Axes.Bottom.TickLabelStyle.FontSize = 10;
                plot.Axes.Bottom.TickLabelStyle.Rotation = -35;
                plot.Axes.Bottom.TickLabelStyle.Alignment = Alignment.MiddleRight;
                plot.Axes.Bottom.TickLabelStyle.ForeColor = Color.FromHex("#111827");

                plot.Axes.Left.Label.Text = $"Przychód ({currency})";
                plot.Axes.Left.Label.FontName = safeFontName;
                plot.Axes.Left.Label.FontSize = 11;
                plot.Axes.Left.Label.Bold = true;
                plot.Axes.Left.Label.ForeColor = Color.FromHex("#111827");

                plot.Axes.Left.TickLabelStyle.FontName = safeFontName;
                plot.Axes.Left.TickLabelStyle.FontSize = 9;
                plot.Axes.Left.TickLabelStyle.ForeColor = Color.FromHex("#111827");

                plot.Axes.SetLimitsX(-0.6, Math.Max(values.Length - 0.4, 1));
                var maxVal = values.Length > 0 && values.Max() > 0 ? values.Max() * 1.15 : 1000;
                plot.Axes.SetLimitsY(0, maxVal);

                plot.Layout.Fixed(new PixelPadding(85, 25, 65, 20));
                plot.Grid.MajorLineColor = Color.FromHex("#e5e7eb");

                var svg = plot.GetSvgXml(850, 360);
                result.Add((currency, svg));
            }

            return result;
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
