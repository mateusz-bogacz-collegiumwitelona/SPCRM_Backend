using Domain.Models;
using Infrastructure.Pdf.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;

namespace Infrastructure.Pdf
{
    public class InvoicePdfGenerator : IInvoicePdfGenerator
    {
        public byte[] GenerateInvoicePdf(Invoice invoice, string language)
        {
            var document = language.ToLower() switch
            {
                "pl" => GenerateInvoicePL(invoice),
                "en" => GenerateInvoiceEN(invoice),
                _ => throw new ArgumentException($"Unsupported language: {language}")
            };

            return document.GeneratePdf();
        }

        private Document GenerateInvoicePL(Invoice invoice)
            => Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text($"Faktura VAT nr {invoice.InvoiceNumber}").Bold().FontSize(18);
                            col.Item().Text($"Data wystawienia: {invoice.IssueDate:yyyy-MM-dd}");
                            col.Item().Text($"Termin płatności: {invoice.DueDate:yyyy-MM-dd}");
                        });

                        row.RelativeItem().AlignRight().Column(col =>
                        {
                            col.Item().Text("SPCRM Sp. z o.o.").Bold();
                            col.Item().Text("NIP: 1234567890");
                            col.Item().Text("ul. Przemysłowa 10, Katowice");
                        });
                    });

                    page.Content().PaddingVertical(20).Column(col =>
                    {
                        col.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).Text("Nabywca:").Bold();
                        col.Item().Text(invoice.Company.Name).FontSize(12).Bold();
                        col.Item().Text($"NIP: {invoice.Company.NIP}");

                        col.Item().PaddingTop(15);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(30);
                                columns.RelativeColumn(4);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Text("Lp.").Bold();
                                header.Cell().Text("Nazwa towaru / usługi").Bold();
                                header.Cell().AlignRight().Text("Ilość").Bold();
                                header.Cell().AlignRight().Text("Cena jedn.").Bold();
                                header.Cell().AlignRight().Text("Wartość").Bold();
                            });

                            int index = 1;
                            if (invoice.Deal?.DealProducts != null)
                            {
                                foreach (var item in invoice.Deal.DealProducts)
                                {
                                    var unitPrice = item.UnitPrice / 10000m;
                                    var lineTotal = (item.Quantity * item.UnitPrice) / 10000m;

                                    table.Cell().Text(index++.ToString());
                                    table.Cell().Text(item.Product?.Name ?? "Pozycja zamówienia");
                                    table.Cell().AlignRight().Text(item.Quantity.ToString());
                                    table.Cell().AlignRight().Text($"{unitPrice:F2} {invoice.Currency.Code}");
                                    table.Cell().AlignRight().Text($"{lineTotal:F2} {invoice.Currency.Code}");
                                }
                            }
                        });

                        col.Item().AlignRight().PaddingTop(15).Column(c =>
                        {
                            var total = invoice.TotalAmount / 10000m;
                            c.Item().Text($"Do zapłaty: {total:F2} {invoice.Currency.Code}").Bold().FontSize(14);
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

        private Document GenerateInvoiceEN(Invoice invoice)
           => Document.Create(container =>
           {
               container.Page(page =>
               {
                   page.Margin(30);
                   page.Size(PageSizes.A4);
                   page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                   page.Header().Row(row =>
                   {
                       row.RelativeItem().Column(col =>
                       {
                           col.Item().Text($"Invoice VAT no. {invoice.InvoiceNumber}").Bold().FontSize(18);
                           col.Item().Text($"Issue date: {invoice.IssueDate:yyyy-MM-dd}");
                           col.Item().Text($"Payment due date: {invoice.DueDate:yyyy-MM-dd}");
                       });

                       row.RelativeItem().AlignRight().Column(col =>
                       {
                           col.Item().Text("SPCRM Sp. z o.o.").Bold();
                           col.Item().Text("NIP: 1234567890");
                           col.Item().Text("ul. Przemysłowa 10, Katowice");
                       });
                   });

                   page.Content().PaddingVertical(20).Column(col =>
                   {
                       col.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).Text("Buyer:").Bold();
                       col.Item().Text(invoice.Company.Name).FontSize(12).Bold();
                       col.Item().Text($"NIP: {invoice.Company.NIP}");

                       col.Item().PaddingTop(15);

                       col.Item().Table(table =>
                       {
                           table.ColumnsDefinition(columns =>
                           {
                               columns.ConstantColumn(30);
                               columns.RelativeColumn(4);
                               columns.RelativeColumn(2);
                               columns.RelativeColumn(2);
                               columns.RelativeColumn(2);
                           });

                           table.Header(header =>
                           {
                               header.Cell().Text("Lp.").Bold();
                               header.Cell().Text("Product Name").Bold();
                               header.Cell().AlignRight().Text("Quantity").Bold();
                               header.Cell().AlignRight().Text("Unit Price").Bold();
                               header.Cell().AlignRight().Text("Total Value").Bold();
                           });

                           int index = 1;
                           if (invoice.Deal?.DealProducts != null)
                           {
                               foreach (var item in invoice.Deal.DealProducts)
                               {
                                   var unitPrice = item.UnitPrice / 10000m;
                                   var lineTotal = (item.Quantity * item.UnitPrice) / 10000m;

                                   table.Cell().Text(index++.ToString());
                                   table.Cell().Text(item.Product?.Name ?? "Order Line Item");
                                   table.Cell().AlignRight().Text(item.Quantity.ToString());
                                   table.Cell().AlignRight().Text($"{unitPrice:F2} {invoice.Currency.Code}");
                                   table.Cell().AlignRight().Text($"{lineTotal:F2} {invoice.Currency.Code}");
                               }
                           }
                       });

                       col.Item().AlignRight().PaddingTop(15).Column(c =>
                       {
                           var total = invoice.TotalAmount / 10000m;
                           c.Item().Text($"Total to pay: {total:F2} {invoice.Currency.Code}").Bold().FontSize(14);
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
    }
}
