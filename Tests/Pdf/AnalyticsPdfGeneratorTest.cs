using Infrastructure.Pdf;
using Infrastructure.Pdf.Command;
using QuestPDF.Infrastructure;

namespace Tests.Pdf
{
    public class AnalyticsPdfGeneratorTest
    {
        private AnalyticsPdfGenerator _generator = null!;
        private static readonly byte[] PdfHeaderBytes = [0x25, 0x50, 0x44, 0x46];

        [Before(Class)]
        public static void SetupClass()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        [Before(Test)]
        public void Setup()
        {
            _generator = new AnalyticsPdfGenerator();
        }

        // ─── GenerateEmployeeReportPdf ──────────────────────────────────────────

        [Test]
        public async Task GenerateEmployeeReportPdf_WhenFullDataProvided_GeneratesValidPdfBytes()
        {
            // Arrange
            var command = new EmployeeAnalyticsReportCommand
            {
                FirstName = "Adam",
                LastName = "Kowalski",
                Email = "adam.kowalski@spcrm.pl",
                PeriodTitle = "Wyniki sprzedaży - I Kwartał 2026",
                WinRatePercentageThisMonth = 68.5m,
                CompletedTasksThisMonth = 42,
                OverdueTasksCount = 3,
                GeneratedAtUtc = DateTime.UtcNow,
                RevenueThisWeek = new List<CurrencyAmountCommand>
                {
                    new() { CurrencyCode = "PLN", Amount = 45000, DecimalPlaces = 2 }
                },
                RevenueThisMonth = new List<CurrencyAmountCommand>
                {
                    new() { CurrencyCode = "PLN", Amount = 180000, DecimalPlaces = 2 },
                    new() { CurrencyCode = "EUR", Amount = 25000, DecimalPlaces = 2 }
                },
                RevenueThisYear = new List<CurrencyAmountCommand>
                {
                    new() { CurrencyCode = "PLN", Amount = 850000, DecimalPlaces = 2 }
                },
                ActiveDealsPipelineValue = new List<CurrencyAmountCommand>
                {
                    new() { CurrencyCode = "PLN", Amount = 320000, DecimalPlaces = 2 }
                },
                HistoryMetrics = new List<HistoryMetricCommand>
                {
                    new()
                    {
                        Label = "Styczeń",
                        DealsWonCount = 4,
                        Revenue = new List<CurrencyAmountCommand>
                        {
                            new() { CurrencyCode = "PLN", Amount = 90000, DecimalPlaces = 2 }
                        }
                    },
                    new()
                    {
                        Label = "Luty",
                        DealsWonCount = 6,
                        Revenue = new List<CurrencyAmountCommand>
                        {
                            new() { CurrencyCode = "PLN", Amount = 120000, DecimalPlaces = 2 }
                        }
                    }
                }
            };

            // Act
            var pdfBytes = _generator.GenerateEmployeeReportPdf(command);

            // Assert
            await Assert.That(pdfBytes).IsNotNull();
            await Assert.That(pdfBytes.Length).IsGreaterThan(1000);
            await Assert.That(pdfBytes[..4]).IsEquivalentTo(PdfHeaderBytes);
        }

        [Test]
        public async Task GenerateEmployeeReportPdf_WhenMinimalDataWithEmptyCollections_GeneratesValidPdfWithoutExceptions()
        {
            // Arrange
            var command = new EmployeeAnalyticsReportCommand
            {
                FirstName = "Nowy",
                LastName = "Pracownik",
                Email = "nowy@spcrm.pl",
                PeriodTitle = "Bieżący miesiąc",
                WinRatePercentageThisMonth = 0m,
                CompletedTasksThisMonth = 0,
                OverdueTasksCount = 0,
                RevenueThisWeek = new List<CurrencyAmountCommand>(),
                RevenueThisMonth = new List<CurrencyAmountCommand>(),
                RevenueThisYear = new List<CurrencyAmountCommand>(),
                ActiveDealsPipelineValue = new List<CurrencyAmountCommand>(),
                HistoryMetrics = new List<HistoryMetricCommand>()
            };

            // Act
            var pdfBytes = _generator.GenerateEmployeeReportPdf(command);

            // Assert
            await Assert.That(pdfBytes).IsNotNull();
            await Assert.That(pdfBytes.Length).IsGreaterThan(0);
            await Assert.That(pdfBytes[..4]).IsEquivalentTo(PdfHeaderBytes);
        }

        [Test]
        public async Task GenerateEmployeeReportPdf_WhenHistoryMetricsContainsMultipleCurrencies_GeneratesPdfWithCharts()
        {
            // Arrange 
            var command = new EmployeeAnalyticsReportCommand
            {
                FirstName = "Piotr",
                LastName = "Zieliński",
                Email = "piotr@spcrm.pl",
                PeriodTitle = "Rok 2026",
                WinRatePercentageThisMonth = 50m,
                CompletedTasksThisMonth = 15,
                OverdueTasksCount = 1,
                HistoryMetrics = new List<HistoryMetricCommand>
                {
                    new()
                    {
                        Label = "Q1",
                        DealsWonCount = 2,
                        Revenue = new List<CurrencyAmountCommand>
                        {
                            new() { CurrencyCode = "PLN", Amount = 100000, DecimalPlaces = 2 },
                            new() { CurrencyCode = "EUR", Amount = 30000, DecimalPlaces = 2 }
                        }
                    },
                    new()
                    {
                        Label = "Q2",
                        DealsWonCount = 3,
                        Revenue = new List<CurrencyAmountCommand>
                        {
                            new() { CurrencyCode = "PLN", Amount = 150000, DecimalPlaces = 2 },
                            new() { CurrencyCode = "EUR", Amount = 40000, DecimalPlaces = 2 }
                        }
                    }
                }
            };

            // Act
            var pdfBytes = _generator.GenerateEmployeeReportPdf(command);

            // Assert
            await Assert.That(pdfBytes).IsNotNull();
            await Assert.That(pdfBytes.Length).IsGreaterThan(2000);
            await Assert.That(pdfBytes[..4]).IsEquivalentTo(PdfHeaderBytes);
        }

        // ─── GenerateTeamReportPdf ──────────────────────────────────────────────

        [Test]
        public async Task GenerateTeamReportPdf_WhenFullDataWithTopPerformersAndHistory_GeneratesValidPdfBytes()
        {
            // Arrange
            var command = new TeamAnalyticsReportCommand
            {
                PeriodTitle = "Podsumowanie Miesięczne - Marzec 2026",
                ActiveDealsCount = 28,
                WonDealsThisMonth = 19,
                LostDealsThisMonth = 5,
                CompletedTasksThisMonth = 120,
                OverdueTasksCount = 8,
                GeneratedAtUtc = DateTime.UtcNow,
                RevenueThisWeek = new List<CurrencyAmountCommand>
                {
                    new() { CurrencyCode = "PLN", Amount = 120000, DecimalPlaces = 2 }
                },
                RevenueThisMonth = new List<CurrencyAmountCommand>
                {
                    new() { CurrencyCode = "PLN", Amount = 540000, DecimalPlaces = 2 },
                    new() { CurrencyCode = "EUR", Amount = 65000, DecimalPlaces = 2 }
                },
                RevenueThisYear = new List<CurrencyAmountCommand>
                {
                    new() { CurrencyCode = "PLN", Amount = 2100000, DecimalPlaces = 2 }
                },
                TopPerformers = new List<TeamLeaderboardRowCommand>
                {
                    new()
                    {
                        FullName = "Jan Kowalski",
                        WonDealsThisMonth = 8,
                        WinRatePercentageThisMonth = 80.0m,
                        RevenueThisMonth = new List<CurrencyAmountCommand>
                        {
                            new() { CurrencyCode = "PLN", Amount = 240000, DecimalPlaces = 2 }
                        }
                    },
                    new()
                    {
                        FullName = "Anna Nowak",
                        WonDealsThisMonth = 6,
                        WinRatePercentageThisMonth = 75.0m,
                        RevenueThisMonth = new List<CurrencyAmountCommand>
                        {
                            new() { CurrencyCode = "PLN", Amount = 190000, DecimalPlaces = 2 }
                        }
                    }
                },
                HistoryMetrics = new List<HistoryMetricCommand>
                {
                    new()
                    {
                        Label = "Tydzień 1",
                        DealsWonCount = 4,
                        Revenue = new List<CurrencyAmountCommand>
                        {
                            new() { CurrencyCode = "PLN", Amount = 110000, DecimalPlaces = 2 }
                        }
                    },
                    new()
                    {
                        Label = "Tydzień 2",
                        DealsWonCount = 5,
                        Revenue = new List<CurrencyAmountCommand>
                        {
                            new() { CurrencyCode = "PLN", Amount = 130000, DecimalPlaces = 2 }
                        }
                    }
                }
            };

            // Act
            var pdfBytes = _generator.GenerateTeamReportPdf(command);

            // Assert
            await Assert.That(pdfBytes).IsNotNull();
            await Assert.That(pdfBytes.Length).IsGreaterThan(1000);
            await Assert.That(pdfBytes[..4]).IsEquivalentTo(PdfHeaderBytes);
        }

        [Test]
        public async Task GenerateTeamReportPdf_WhenTopPerformersAndHistoryAreEmpty_GeneratesValidPdfWithoutCrashing()
        {
            // Arrange 
            var command = new TeamAnalyticsReportCommand
            {
                PeriodTitle = "Nowy Zespół",
                ActiveDealsCount = 0,
                WonDealsThisMonth = 0,
                LostDealsThisMonth = 0,
                CompletedTasksThisMonth = 0,
                OverdueTasksCount = 0,
                RevenueThisWeek = new List<CurrencyAmountCommand>(),
                RevenueThisMonth = new List<CurrencyAmountCommand>(),
                RevenueThisYear = new List<CurrencyAmountCommand>(),
                TopPerformers = new List<TeamLeaderboardRowCommand>(),
                HistoryMetrics = new List<HistoryMetricCommand>()
            };

            // Act
            var pdfBytes = _generator.GenerateTeamReportPdf(command);

            // Assert
            await Assert.That(pdfBytes).IsNotNull();
            await Assert.That(pdfBytes.Length).IsGreaterThan(0);
            await Assert.That(pdfBytes[..4]).IsEquivalentTo(PdfHeaderBytes);
        }
    }
}
