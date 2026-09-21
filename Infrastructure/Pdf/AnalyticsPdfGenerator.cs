using Domain.Constants;
using Infrastructure.Pdf.Command;
using Infrastructure.Pdf.Helpers;
using Infrastructure.Pdf.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;

namespace Infrastructure.Pdf
{
    public class AnalyticsPdfGenerator : IAnalyticsPdfGenerator
    {
        public byte[] GenerateEmployeeReportPdf(EmployeeAnalyticsReportCommand data)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem(3).Column(col =>
                        {
                            col.Item().Text("Raport Efektywności Handlowca").Bold().FontSize(18).FontColor(Colors.Blue.Darken3);
                            col.Item().Text($"Pracownik: {data.FirstName} {data.LastName}").Bold().FontSize(12);
                            col.Item().Text($"Email: {data.Email}");
                            col.Item().Text($"Wygenerowano: {data.GeneratedAtUtc:yyyy.MM.dd HH:mm}");
                        });

                        row.RelativeItem(2).AlignRight().Column(col =>
                        {
                            col.Item().Text("SPCRM Sp. z o.o.").Bold();
                            col.Item().Text("Dział Sprzedaży");
                            col.Item().Text("ul. Przemysłowa 10, Katowice");
                        });
                    });

                    page.Content().PaddingVertical(15).Column(col =>
                    {
                        col.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5)
                            .Text("Kluczowe Wskaźniki Efektywności (KPI)").Bold().FontSize(12);

                        col.Item().PaddingTop(10).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });

                            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3).Padding(8).Column(c =>
                            {
                                c.Item().Text("Przychód (Tydzień)").FontSize(9).FontColor(Colors.Grey.Medium);
                                c.Item().Text(FormatCurrencies(data.RevenueThisWeek)).Bold().FontSize(12);
                            });

                            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3).Padding(8).Column(c =>
                            {
                                c.Item().Text("Przychód (Miesiąc)").FontSize(9).FontColor(Colors.Grey.Medium);
                                c.Item().Text(FormatCurrencies(data.RevenueThisMonth)).Bold().FontSize(12);
                            });

                            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3).Padding(8).Column(c =>
                            {
                                c.Item().Text("Przychód (Rok)").FontSize(9).FontColor(Colors.Grey.Medium);
                                c.Item().Text(FormatCurrencies(data.RevenueThisYear)).Bold().FontSize(12);
                            });

                            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3).Padding(8).Column(c =>
                            {
                                c.Item().Text("Skuteczność (Win Rate)").FontSize(9).FontColor(Colors.Grey.Medium);
                                c.Item().Text($"{data.WinRatePercentageThisMonth:N2}%").Bold().FontSize(12);
                            });

                            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3).Padding(8).Column(c =>
                            {
                                c.Item().Text("Wartość otwartych szans").FontSize(9).FontColor(Colors.Grey.Medium);
                                c.Item().Text(FormatCurrencies(data.ActiveDealsPipelineValue)).Bold().FontSize(12);
                            });

                            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3).Padding(8).Column(c =>
                            {
                                c.Item().Text("Zadania (Zrobione / Zaległe)").FontSize(9).FontColor(Colors.Grey.Medium);
                                c.Item().Text($"{data.CompletedTasksThisMonth} / {data.OverdueTasksCount}").Bold().FontSize(12);
                            });
                        });

                        col.Item().PaddingTop(20);

                        col.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5)
                            .Text(data.PeriodTitle).Bold().FontSize(12);

                        col.Item().PaddingTop(10).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(4);
                                columns.RelativeColumn(2);
                            });

                            table.Header(header =>
                            {
                                header.Cell().BorderBottom(1.5f).BorderColor(Colors.Grey.Darken1).Padding(5).Text("Okres").Bold();
                                header.Cell().BorderBottom(1.5f).BorderColor(Colors.Grey.Darken1).AlignRight().Padding(5).Text("Przychód").Bold();
                                header.Cell().BorderBottom(1.5f).BorderColor(Colors.Grey.Darken1).AlignRight().Padding(5).Text("Zamknięte tematy").Bold();
                            });

                            foreach (var metric in data.HistoryMetrics ?? [])
                            {
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(metric.Label);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).AlignRight().Padding(5).Text(FormatCurrencies(metric.Revenue));
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).AlignRight().Padding(5).Text(metric.DealsWonCount.ToString());
                            }
                        });

                        if (data.HistoryMetrics != null && data.HistoryMetrics.Any())
                        {
                            col.Item().PageBreak();

                            col.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5)
                                .Text($"Wykres Sprzedaży - {data.PeriodTitle}").Bold().FontSize(12);

                            var chartSvg = AnalyticsChartPdfHelper.GenerateRevenueChartImage(data.HistoryMetrics);

                            col.Item().PaddingTop(25).Element(c =>
                            {
                                c.Width(520).Svg(chartSvg).FitWidth();
                            });
                        }
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
                });
            });

            return document.GeneratePdf();
        }

        public byte[] GenerateTeamReportPdf(TeamAnalyticsReportCommand data)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem(3).Column(col =>
                        {
                            col.Item().Text("Raport Sprzedaży Zespołu").Bold().FontSize(18).FontColor(Colors.Blue.Darken3);
                            col.Item().Text("Podsumowanie Działu Handlowego").Bold().FontSize(12);
                            col.Item().Text($"Wygenerowano: {data.GeneratedAtUtc:yyyy.MM.dd HH:mm}");
                        });

                        row.RelativeItem(2).AlignRight().Column(col =>
                        {
                            col.Item().Text("SPCRM Sp. z o.o.").Bold();
                            col.Item().Text("Zarząd & Dyrekcja Sprzedaży");
                            col.Item().Text("ul. Przemysłowa 10, Katowice");
                        });
                    });

                    page.Content().PaddingVertical(15).Column(col =>
                    {
                        col.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5)
                            .Text("Kluczowe Wskaźniki Efektywności Zespołu (KPI)").Bold().FontSize(12);

                        col.Item().PaddingTop(10).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });

                            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3).Padding(8).Column(c =>
                            {
                                c.Item().Text("Przychód Zespołu (Tydzień)").FontSize(9).FontColor(Colors.Grey.Medium);
                                c.Item().Text(FormatCurrencies(data.RevenueThisWeek)).Bold().FontSize(12);
                            });

                            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3).Padding(8).Column(c =>
                            {
                                c.Item().Text("Przychód Zespołu (Miesiąc)").FontSize(9).FontColor(Colors.Grey.Medium);
                                c.Item().Text(FormatCurrencies(data.RevenueThisMonth)).Bold().FontSize(12);
                            });

                            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3).Padding(8).Column(c =>
                            {
                                c.Item().Text("Przychód Zespołu (Rok)").FontSize(9).FontColor(Colors.Grey.Medium);
                                c.Item().Text(FormatCurrencies(data.RevenueThisYear)).Bold().FontSize(12);
                            });

                            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3).Padding(8).Column(c =>
                            {
                                c.Item().Text("Aktywne transakcje").FontSize(9).FontColor(Colors.Grey.Medium);
                                c.Item().Text($"{data.ActiveDealsCount} tematów").Bold().FontSize(12);
                            });

                            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3).Padding(8).Column(c =>
                            {
                                c.Item().Text("Zamknięte transakcje (Miesiąc)").FontSize(9).FontColor(Colors.Grey.Medium);
                                c.Item().Text($"{data.WonDealsThisMonth} wygranych / {data.LostDealsThisMonth} straconych").Bold().FontSize(12);
                            });

                            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3).Padding(8).Column(c =>
                            {
                                c.Item().Text("Zadania Zespołu (Zrobione / Zaległe)").FontSize(9).FontColor(Colors.Grey.Medium);
                                c.Item().Text($"{data.CompletedTasksThisMonth} / {data.OverdueTasksCount}").Bold().FontSize(12);
                            });
                        });

                        col.Item().PaddingTop(20);

                        if (data.TopPerformers is { Count: > 0 })
                        {
                            col.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5)
                                .Text("Ranking Handlowców (Bieżący miesiąc)").Bold().FontSize(12);

                            col.Item().PaddingTop(10).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(25);
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().BorderBottom(1.5f).BorderColor(Colors.Grey.Darken1).Padding(5).Text("Lp.").Bold();
                                    header.Cell().BorderBottom(1.5f).BorderColor(Colors.Grey.Darken1).Padding(5).Text("Handlowiec").Bold();
                                    header.Cell().BorderBottom(1.5f).BorderColor(Colors.Grey.Darken1).AlignRight().Padding(5).Text("Przychód").Bold();
                                    header.Cell().BorderBottom(1.5f).BorderColor(Colors.Grey.Darken1).AlignRight().Padding(5).Text("Wygrane").Bold();
                                    header.Cell().BorderBottom(1.5f).BorderColor(Colors.Grey.Darken1).AlignRight().Padding(5).Text("Skuteczność").Bold();
                                });

                                int lp = 1;
                                foreach (var performer in data.TopPerformers)
                                {
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text($"{lp++}.");
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(performer.FullName);
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).AlignRight().Padding(5).Text(FormatCurrencies(performer.RevenueThisMonth));
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).AlignRight().Padding(5).Text(performer.WonDealsThisMonth.ToString());
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).AlignRight().Padding(5).Text($"{performer.WinRatePercentageThisMonth:N2}%");
                                }
                            });
                        }

                        if (data.HistoryMetrics is { Count: > 0 })
                        {
                            col.Item().PageBreak();

                            col.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5)
                                .Text($"Wykres Sprzedaży Zespołu - {data.PeriodTitle}").Bold().FontSize(12);

                            var chartSvg = AnalyticsChartPdfHelper.GenerateRevenueChartImage(data.HistoryMetrics);

                            col.Item().PaddingTop(25).Element(c =>
                            {
                                c.Width(520).Svg(chartSvg).FitWidth();
                            });
                        }
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
                });
            });

            return document.GeneratePdf();
        }

        private static string FormatCurrencies(IEnumerable<CurrencyAmountCommand>? amounts)
        {
            if (amounts == null)
            {
                return $"0.00 {BusinessConstants.DefaultCurrencyCode}";
            }

            var list = amounts.ToList();
            if (list.Count == 0)
            {
                return $"0.00 {BusinessConstants.DefaultCurrencyCode}";
            }

            return string.Join("\n", list.Select(a => $"{a.Amount:N2} {a.CurrencyCode}"));
        }
    }
}
