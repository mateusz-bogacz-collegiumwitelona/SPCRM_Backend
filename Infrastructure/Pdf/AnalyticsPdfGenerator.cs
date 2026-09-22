using Domain.Constants;
using Infrastructure.Pdf.Command;
using Infrastructure.Pdf.Helpers;
using Infrastructure.Pdf.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

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

                        col.Item().PaddingTop(10).Row(row =>
                        {
                            row.Spacing(10);
                            row.RelativeItem().Element(c => RenderCurrencyPeriodBlock(c, "Przychód (Tydzień)", data.RevenueThisWeek));
                            row.RelativeItem().Element(c => RenderCurrencyPeriodBlock(c, "Przychód (Miesiąc)", data.RevenueThisMonth));
                            row.RelativeItem().Element(c => RenderCurrencyPeriodBlock(c, "Przychód (Rok)", data.RevenueThisYear));
                        });

                        col.Item().PaddingTop(12).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });

                            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3).Padding(6).Column(c =>
                            {
                                c.Item().Text("Skuteczność (Win Rate)").FontSize(9).FontColor(Colors.Grey.Medium);
                                c.Item().Text($"{data.WinRatePercentageThisMonth:N2}%").Bold().FontSize(12);
                            });

                            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3).Padding(6).Column(c =>
                            {
                                c.Item().Text("Wartość otwartych szans").FontSize(9).FontColor(Colors.Grey.Medium);
                                RenderCurrencyTable(c.Item().PaddingTop(4), data.ActiveDealsPipelineValue);
                            });

                            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3).Padding(6).Column(c =>
                            {
                                c.Item().Text("Zadania (Zrobione / Zaległe)").FontSize(9).FontColor(Colors.Grey.Medium);
                                c.Item().Text($"{data.CompletedTasksThisMonth} / {data.OverdueTasksCount}").Bold().FontSize(12);
                            });
                        });

                        col.Item().PaddingTop(20);

                        col.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5)
                            .Text(data.PeriodTitle).Bold().FontSize(12);

                        // Tabela z historią i sumami per waluta
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
                                header.Cell().BorderBottom(1.5f).BorderColor(Colors.Grey.Darken1).Padding(5).Text("Przychód").Bold();
                                header.Cell().BorderBottom(1.5f).BorderColor(Colors.Grey.Darken1).AlignRight().Padding(5).Text("Zamknięte tematy").Bold();
                            });

                            foreach (var metric in data.HistoryMetrics ?? [])
                            {
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(metric.Label);
                                RenderCurrencyTable(table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5), metric.Revenue);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).AlignRight().Padding(5).Text(metric.DealsWonCount.ToString());
                            }

                            // Suma łączna walut w stopce tabeli
                            var currencyTotals = (data.HistoryMetrics ?? Enumerable.Empty<HistoryMetricCommand>())
                                .SelectMany(m => m.Revenue)
                                .GroupBy(r => new { r.CurrencyCode, r.DecimalPlaces })
                                .Select(g => new CurrencyAmountCommand
                                {
                                    CurrencyCode = g.Key.CurrencyCode,
                                    DecimalPlaces = g.Key.DecimalPlaces,
                                    Amount = g.Sum(x => x.Amount)
                                })
                                .ToList();

                            var totalDeals = (data.HistoryMetrics ?? Enumerable.Empty<HistoryMetricCommand>()).Sum(m => m.DealsWonCount);

                            table.Cell().BorderTop(1.5f).BorderColor(Colors.Grey.Darken1).Padding(5).Text("Suma").Bold();
                            RenderCurrencyTable(table.Cell().BorderTop(1.5f).BorderColor(Colors.Grey.Darken1).Padding(5), currencyTotals, isBold: true);
                            table.Cell().BorderTop(1.5f).BorderColor(Colors.Grey.Darken1).AlignRight().Padding(5).Text(totalDeals.ToString()).Bold();
                        });

                        // Diagramy - jeden wykres dla każdej waluty
                        if (data.HistoryMetrics != null && data.HistoryMetrics.Any())
                        {
                            var charts = AnalyticsChartPdfHelper.GenerateRevenueChartsPerCurrency(data.HistoryMetrics);

                            foreach (var (currency, svg) in charts)
                            {
                                col.Item().PageBreak();

                                col.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5)
                                    .Text($"Wykres Sprzedaży ({currency}) - {data.PeriodTitle}").Bold().FontSize(12);

                                col.Item().PaddingTop(20).Element(c =>
                                {
                                    c.Width(520).Svg(svg).FitWidth();
                                });
                            }
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

                        col.Item().PaddingTop(10).Row(row =>
                        {
                            row.Spacing(10);
                            row.RelativeItem().Element(c => RenderCurrencyPeriodBlock(c, "Przychód Zespołu (Tydzień)", data.RevenueThisWeek));
                            row.RelativeItem().Element(c => RenderCurrencyPeriodBlock(c, "Przychód Zespołu (Miesiąc)", data.RevenueThisMonth));
                            row.RelativeItem().Element(c => RenderCurrencyPeriodBlock(c, "Przychód Zespołu (Rok)", data.RevenueThisYear));
                        });

                        col.Item().PaddingTop(12).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });

                            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3).Padding(6).Column(c =>
                            {
                                c.Item().Text("Aktywne transakcje").FontSize(9).FontColor(Colors.Grey.Medium);
                                c.Item().Text($"{data.ActiveDealsCount} tematów").Bold().FontSize(12);
                            });

                            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3).Padding(6).Column(c =>
                            {
                                c.Item().Text("Zamknięte transakcje (Miesiąc)").FontSize(9).FontColor(Colors.Grey.Medium);
                                c.Item().Text($"{data.WonDealsThisMonth} wygranych / {data.LostDealsThisMonth} straconych").Bold().FontSize(12);
                            });

                            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3).Padding(6).Column(c =>
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
                                    header.Cell().BorderBottom(1.5f).BorderColor(Colors.Grey.Darken1).Padding(5).Text("Przychód").Bold();
                                    header.Cell().BorderBottom(1.5f).BorderColor(Colors.Grey.Darken1).AlignRight().Padding(5).Text("Wygrane").Bold();
                                    header.Cell().BorderBottom(1.5f).BorderColor(Colors.Grey.Darken1).AlignRight().Padding(5).Text("Skuteczność").Bold();
                                });

                                int lp = 1;
                                foreach (var performer in data.TopPerformers)
                                {
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text($"{lp++}.");
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(performer.FullName);
                                    RenderCurrencyTable(table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5), performer.RevenueThisMonth);
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).AlignRight().Padding(5).Text(performer.WonDealsThisMonth.ToString());
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).AlignRight().Padding(5).Text($"{performer.WinRatePercentageThisMonth:N2}%");
                                }
                            });
                        }

                        // Wykresy zespołu per waluta
                        if (data.HistoryMetrics is { Count: > 0 })
                        {
                            var charts = AnalyticsChartPdfHelper.GenerateRevenueChartsPerCurrency(data.HistoryMetrics);

                            foreach (var (currency, svg) in charts)
                            {
                                col.Item().PageBreak();

                                col.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5)
                                    .Text($"Wykres Sprzedaży Zespołu ({currency}) - {data.PeriodTitle}").Bold().FontSize(12);

                                col.Item().PaddingTop(20).Element(c =>
                                {
                                    c.Width(520).Svg(svg).FitWidth();
                                });
                            }
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

        private static void RenderCurrencyPeriodBlock(IContainer container, string title, IEnumerable<CurrencyAmountCommand>? amounts)
        {
            container.Column(col =>
            {
                col.Item().Text(title).FontSize(9).FontColor(Colors.Grey.Medium);
                col.Item().PaddingTop(4).Border(1).BorderColor(Colors.Grey.Lighten3).Padding(6)
                    .Element(c => RenderCurrencyTable(c, amounts));
            });
        }

        private static void RenderCurrencyTable(IContainer container, IEnumerable<CurrencyAmountCommand>? amounts, bool isBold = false)
        {
            var list = amounts?.ToList() ?? new List<CurrencyAmountCommand>();

            if (list.Count == 0)
            {
                list.Add(new CurrencyAmountCommand
                {
                    CurrencyCode = BusinessConstants.DefaultCurrencyCode,
                    DecimalPlaces = 2,
                    Amount = 0
                });
            }

            container.Table(t =>
            {
                t.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(3);
                    cols.RelativeColumn(2);
                });

                t.Header(header =>
                {
                    header.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten4)
                        .AlignRight().Padding(3)
                        .Text("Kwota").FontSize(8).FontColor(Colors.Grey.Darken1);
                    header.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten4)
                        .AlignLeft().Padding(3)
                        .Text("Waluta").FontSize(8).FontColor(Colors.Grey.Darken1);
                });

                foreach (var a in list)
                {
                    var format = $"N{a.DecimalPlaces}";
                    var amountText = a.Amount.ToString(format);

                    var cellAmount = t.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).AlignRight().Padding(3).Text(amountText);
                    var cellCurrency = t.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).AlignLeft().Padding(3).Text(a.CurrencyCode);

                    if (isBold)
                    {
                        cellAmount.Bold();
                        cellCurrency.Bold();
                    }
                }
            });
        }
    }
}
