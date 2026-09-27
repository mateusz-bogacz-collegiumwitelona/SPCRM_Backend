using Domain.Models;
using Infrastructure.Pdf;
using QuestPDF.Infrastructure;

namespace Tests.Pdf
{
    public class InvoicePdfGeneratorTest
    {
        private InvoicePdfGenerator _generator = null!;
        private static readonly byte[] PdfHeaderBytes = [0x25, 0x50, 0x44, 0x46]; // %PDF

        [Before(Class)]
        public static void SetupClass()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        [Before(Test)]
        public void Setup()
        {
            _generator = new InvoicePdfGenerator();
        }

        private static Invoice CreateSampleInvoice(bool withProducts = true, string? steelGrade = "1.4301")
        {
            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Polski Złoty",
                Code = "PLN",
                DecimalPlaces = 2
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = "Stal-Bud Sp. z o.o.",
                NIP = "1234567890"
            };

            var invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = "FV/2026/09/0001",
                IssueDate = new DateTime(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc),
                DueDate = new DateTime(2026, 10, 4, 10, 0, 0, DateTimeKind.Utc),
                TotalAmount = 5000000,
                CurrencyId = currency.Id,
                Currency = currency,
                CompanyId = company.Id,
                Company = company,
                InvoiceProducts = new List<InvoiceProducts>()
            };

            if (withProducts)
            {
                invoice.InvoiceProducts.Add(new InvoiceProducts
                {
                    Id = Guid.NewGuid(),
                    InvoiceId = invoice.Id,
                    ProductName = "Rura Nierdzewna",
                    SteelGrade = steelGrade,
                    Quantity = 5,
                    UnitSymbol = "szt.",
                    UnitPrice = 1000000
                });

                invoice.InvoiceProducts.Add(new InvoiceProducts
                {
                    Id = Guid.NewGuid(),
                    InvoiceId = invoice.Id,
                    ProductName = "Blacha Kwasoodporna",
                    SteelGrade = null,
                    Quantity = 2,
                    UnitSymbol = "ark.",
                    UnitPrice = 2500000
                });
            }

            return invoice;
        }


        // ─── GenerateInvoicePdf ──────────────────────────────────────────────


        [Test]
        [Arguments("de")]
        [Arguments("fr")]
        [Arguments("es")]
        [Arguments("")]
        [Arguments("unsupported")]
        public async Task GenerateInvoicePdf_WhenLanguageIsNotSupported_ThrowsArgumentException(string unsupportedLanguage)
        {
            // Arrange
            var invoice = CreateSampleInvoice();

            // Act & Assert
            await Assert.That(() => _generator.GenerateInvoicePdf(invoice, unsupportedLanguage))
                .Throws<ArgumentException>();
        }

        [Test]
        [Arguments("pl")]
        [Arguments("PL")]
        [Arguments("Pl")]
        public async Task GenerateInvoicePdf_WhenPolishLanguageRequested_GeneratesValidPdfBytes(string lang)
        {
            // Arrange
            var invoice = CreateSampleInvoice(withProducts: true);

            // Act
            var pdfBytes = _generator.GenerateInvoicePdf(invoice, lang);

            // Assert
            await Assert.That(pdfBytes).IsNotNull();
            await Assert.That(pdfBytes.Length).IsGreaterThan(1000);
            await Assert.That(pdfBytes[..4]).IsEquivalentTo(PdfHeaderBytes);
        }

        [Test]
        [Arguments("en")]
        [Arguments("EN")]
        [Arguments("En")]
        public async Task GenerateInvoicePdf_WhenEnglishLanguageRequested_GeneratesValidPdfBytes(string lang)
        {
            // Arrange
            var invoice = CreateSampleInvoice(withProducts: true);

            // Act
            var pdfBytes = _generator.GenerateInvoicePdf(invoice, lang);

            // Assert
            await Assert.That(pdfBytes).IsNotNull();
            await Assert.That(pdfBytes.Length).IsGreaterThan(1000);
            await Assert.That(pdfBytes[..4]).IsEquivalentTo(PdfHeaderBytes);
        }


        [Test]
        public async Task GenerateInvoicePdf_WhenInvoiceHasNoProducts_GeneratesPdfUsingTotalAmountFallback()
        {
            // Arrange
            var invoice = CreateSampleInvoice(withProducts: false);

            // Act
            var pdfBytes = _generator.GenerateInvoicePdf(invoice, "pl");

            // Assert
            await Assert.That(pdfBytes).IsNotNull();
            await Assert.That(pdfBytes.Length).IsGreaterThan(0);
            await Assert.That(pdfBytes[..4]).IsEquivalentTo(PdfHeaderBytes);
        }

        [Test]
        public async Task GenerateInvoicePdf_WhenInvoiceProductsIsNull_GeneratesPdfWithoutNullReferenceException()
        {
            // Arrange
            var invoice = CreateSampleInvoice(withProducts: false);
            invoice.InvoiceProducts = null!;

            // Act
            var pdfBytes = _generator.GenerateInvoicePdf(invoice, "pl");

            // Assert
            await Assert.That(pdfBytes).IsNotNull();
            await Assert.That(pdfBytes.Length).IsGreaterThan(0);
            await Assert.That(pdfBytes[..4]).IsEquivalentTo(PdfHeaderBytes);
        }

        [Test]
        public async Task GenerateInvoicePdf_WhenProductSteelGradeIsWhitespace_OmitsSteelGradeWithoutCrashing()
        {
            // Arrange
            var invoice = CreateSampleInvoice(withProducts: true, steelGrade: "   ");

            // Act
            var pdfBytes = _generator.GenerateInvoicePdf(invoice, "pl");

            // Assert
            await Assert.That(pdfBytes).IsNotNull();
            await Assert.That(pdfBytes.Length).IsGreaterThan(0);
            await Assert.That(pdfBytes[..4]).IsEquivalentTo(PdfHeaderBytes);
        }
    }
}
