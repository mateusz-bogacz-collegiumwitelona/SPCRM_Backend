using Infrastructure.Pdf.Command;
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
                            col.Item().Text($"Wygenerowano: {data.GeneratedAtUtc:yyyy-MM-dd HH:mm} UTC");
                        });

                        row.RelativeItem(2).AlignRight().Column(col =>
                        {
                            col.Item().Text("SPCRM Sp. z o.o.").Bold();
                            col.Item().Text("Dział Sprzedaży");
                            col.Item().Text("ul. Przemysłowa 10, Katowice");
                        });
                    });

                    page.Content().PaddingVertical(20).Column(col =>
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
                                c.Item().Text($"{data.RevenueThisWeek:N2} PLN").Bold().FontSize(12);
                            });

                            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3).Padding(8).Column(c =>
                            {
                                c.Item().Text("Przychód (Miesiąc)").FontSize(9).FontColor(Colors.Grey.Medium);
                                c.Item().Text($"{data.RevenueThisMonth:N2} PLN").Bold().FontSize(12);
                            });

                            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3).Padding(8).Column(c =>
                            {
                                c.Item().Text("Przychód (Rok)").FontSize(9).FontColor(Colors.Grey.Medium);
                                c.Item().Text($"{data.RevenueThisYear:N2} PLN").Bold().FontSize(12);
                            });

                            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3).Padding(8).Column(c =>
                            {
                                c.Item().Text("Skuteczność (Win Rate)").FontSize(9).FontColor(Colors.Grey.Medium);
                                c.Item().Text($"{data.WinRatePercentageThisMonth:N2}%").Bold().FontSize(12);
                            });

                            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3).Padding(8).Column(c =>
                            {
                                c.Item().Text("Aktywny Lejek").FontSize(9).FontColor(Colors.Grey.Medium);
                                c.Item().Text($"{data.ActiveDealsPipelineValue:N2} PLN").Bold().FontSize(12);
                            });

                            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten3).Padding(8).Column(c =>
                            {
                                c.Item().Text("Zadania (Zrobione / Zaległe)").FontSize(9).FontColor(Colors.Grey.Medium);
                                c.Item().Text($"{data.CompletedTasksThisMonth} / {data.OverdueTasksCount}").Bold().FontSize(12);
                            });
                        });

                        col.Item().PaddingTop(25);

                        col.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5)
                            .Text(data.PeriodTitle).Bold().FontSize(12);

                        col.Item().PaddingTop(10).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(2);
                            });

                            table.Header(header =>
                            {
                                header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Medium).Padding(5).Text("Okres").Bold();
                                header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Medium).AlignRight().Padding(5).Text("Przychód").Bold();
                                header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Medium).AlignRight().Padding(5).Text("Zamknięte tematy").Bold();
                            });

                            foreach (var metric in data.HistoryMetrics)
                            {
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(metric.Label);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).AlignRight().Padding(5).Text($"{metric.Revenue:N2} PLN");
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).AlignRight().Padding(5).Text(metric.DealsWonCount.ToString());
                            }
                        });
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
    }
}
