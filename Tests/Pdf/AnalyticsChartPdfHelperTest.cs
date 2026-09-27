using Domain.Constants;
using Infrastructure.Pdf.Command;
using Infrastructure.Pdf.Helpers;

namespace Tests.Pdf
{
    public class AnalyticsChartPdfHelperTest
    {
        // ─── GenerateRevenueChartsPerCurrency ─────────────────────────────────────────────────

        [Test]
        public async Task GenerateRevenueChartsPerCurrency_WhenHistoryCommandIsNull_ReturnsEmptyList()
        {
            // Act
            var result = AnalyticsChartPdfHelper.GenerateRevenueChartsPerCurrency(null!);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result).IsEmpty();
        }

        [Test]
        public async Task GenerateRevenueChartsPerCurrency_WhenHistoryCommandIsEmpty_ReturnsEmptyList()
        {
            // Arrange
            var emptyList = new List<HistoryMetricCommand>();

            // Act
            var result = AnalyticsChartPdfHelper.GenerateRevenueChartsPerCurrency(emptyList);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result).IsEmpty();
        }

        [Test]
        public async Task GenerateRevenueChartsPerCurrency_WhenSingleCurrencyProvided_GeneratesSingleValidSvgChart()
        {
            // Arrange
            var history = new List<HistoryMetricCommand>
            {
                new()
                {
                    Label = "Styczeń 2026",
                    DealsWonCount = 3,
                    Revenue = new List<CurrencyAmountCommand>
                    {
                        new() { CurrencyCode = "PLN", Amount = 150000, DecimalPlaces = 2 }
                    }
                },
                new()
                {
                    Label = "Luty 2026",
                    DealsWonCount = 5,
                    Revenue = new List<CurrencyAmountCommand>
                    {
                        new() { CurrencyCode = "PLN", Amount = 230000, DecimalPlaces = 2 }
                    }
                }
            };

            // Act
            var result = AnalyticsChartPdfHelper.GenerateRevenueChartsPerCurrency(history);

            // Assert
            await Assert.That(result).Count().IsEqualTo(1);

            var (currencyCode, svg) = result[0];
            await Assert.That(currencyCode).IsEqualTo("PLN");
            await Assert.That(svg).IsNotNull();
            await Assert.That(svg).Contains("<svg");
            await Assert.That(svg).Contains("</svg>");
        }

        [Test]
        public async Task GenerateRevenueChartsPerCurrency_WhenMultipleCurrenciesProvided_GeneratesChartForEachCurrencySortedAlphabetically()
        {
            // Arrange 
            var history = new List<HistoryMetricCommand>
            {
                new()
                {
                    Label = "Q1",
                    DealsWonCount = 4,
                    Revenue = new List<CurrencyAmountCommand>
                    {
                        new() { CurrencyCode = "PLN", Amount = 500000, DecimalPlaces = 2 },
                        new() { CurrencyCode = "EUR", Amount = 120000, DecimalPlaces = 2 }
                    }
                },
                new()
                {
                    Label = "Q2",
                    DealsWonCount = 6,
                    Revenue = new List<CurrencyAmountCommand>
                    {
                        new() { CurrencyCode = "PLN", Amount = 750000, DecimalPlaces = 2 },
                        new() { CurrencyCode = "EUR", Amount = 180000, DecimalPlaces = 2 }
                    }
                }
            };

            // Act
            var result = AnalyticsChartPdfHelper.GenerateRevenueChartsPerCurrency(history);

            // Assert
            await Assert.That(result).Count().IsEqualTo(2);

            await Assert.That(result[0].CurrencyCode).IsEqualTo("EUR");
            await Assert.That(result[0].svg).Contains("<svg");
            await Assert.That(result[0].svg).Contains("</svg>");

            await Assert.That(result[1].CurrencyCode).IsEqualTo("PLN");
            await Assert.That(result[1].svg).Contains("<svg");
            await Assert.That(result[1].svg).Contains("</svg>");
        }

        [Test]
        public async Task GenerateRevenueChartsPerCurrency_WhenCurrencyMissingInOnePeriod_FillsZeroAndGeneratesWithoutError()
        {
            // Arrange
            var history = new List<HistoryMetricCommand>
            {
                new()
                {
                    Label = "Styczeń",
                    DealsWonCount = 2,
                    Revenue = new List<CurrencyAmountCommand>
                    {
                        new() { CurrencyCode = "PLN", Amount = 10000, DecimalPlaces = 2 }
                    }
                },
                new()
                {
                    Label = "Luty",
                    DealsWonCount = 3,
                    Revenue = new List<CurrencyAmountCommand>
                    {
                        new() { CurrencyCode = "PLN", Amount = 20000, DecimalPlaces = 2 },
                        new() { CurrencyCode = "EUR", Amount = 5000, DecimalPlaces = 2 }
                    }
                }
            };

            // Act
            var result = AnalyticsChartPdfHelper.GenerateRevenueChartsPerCurrency(history);

            // Assert
            await Assert.That(result).Count().IsEqualTo(2);

            var eurChart = result.First(r => r.CurrencyCode == "EUR");
            await Assert.That(eurChart.svg).IsNotNull();
            await Assert.That(eurChart.svg).Contains("<svg");
            await Assert.That(eurChart.svg).Contains("</svg>");
        }

        [Test]
        public async Task GenerateRevenueChartsPerCurrency_WhenNoRevenuesInsideMetrics_UsesDefaultCurrencyCodeFallback()
        {
            // Arrange 
            var history = new List<HistoryMetricCommand>
            {
                new()
                {
                    Label = "Styczeń",
                    DealsWonCount = 0,
                    Revenue = new List<CurrencyAmountCommand>()
                },
                new()
                {
                    Label = "Luty",
                    DealsWonCount = 0,
                    Revenue = new List<CurrencyAmountCommand>()
                }
            };

            // Act
            var result = AnalyticsChartPdfHelper.GenerateRevenueChartsPerCurrency(history);

            // Assert
            await Assert.That(result).Count().IsEqualTo(1);
            await Assert.That(result[0].CurrencyCode).IsEqualTo(BusinessConstants.DefaultCurrencyCode);
            await Assert.That(result[0].svg).Contains("<svg");
            await Assert.That(result[0].svg).Contains("</svg>");
        }

        [Test]
        public async Task GenerateRevenueChartsPerCurrency_WhenAllAmountsAreZero_GeneratesChartWithFallbackLimits()
        {
            // Arrange 
            var history = new List<HistoryMetricCommand>
            {
                new()
                {
                    Label = "Tydzień 1",
                    DealsWonCount = 0,
                    Revenue = new List<CurrencyAmountCommand>
                    {
                        new() { CurrencyCode = "PLN", Amount = 0, DecimalPlaces = 2 }
                    }
                }
            };

            // Act
            var result = AnalyticsChartPdfHelper.GenerateRevenueChartsPerCurrency(history);

            // Assert
            await Assert.That(result).Count().IsEqualTo(1);
            await Assert.That(result[0].svg).IsNotNull();
            await Assert.That(result[0].svg).Contains("<svg");
            await Assert.That(result[0].svg).Contains("</svg>");
        }
    }
}
