using Domain.Enum;
using Domain.Models;
using Infrastructure;
using Infrastructure.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Testcontainers.PostgreSql;
using Tests.Fakes;
using Worker.Workers;

namespace Tests.Workers
{
    public class InvoiceEmailWorkerTest
    {
        private AppDbContext _contextMock = null!;
        private static PostgreSqlContainer _dbContainer = null!;
        private static string _connectionString = null!;
        private string _currentSchema = null!;

        private FakeInvoicePdfGenerator _pdfGeneratorFake = null!;
        private FakeSmtpEmailService _smtpServiceFake = null!;
        private ILogger<InvoiceEmailWorker> _loggerMock = null!;
        private InvoiceEmailWorker _worker = null!;

        [Before(Class)]
        [Obsolete]
        public static async Task SetupClassAsync()
        {
            _dbContainer = new PostgreSqlBuilder()
                .WithImage("postgis/postgis:18-3.6")
                .WithDatabase("testdb")
                .WithUsername("testuser")
                .WithPassword("testpassword")
                .WithCommand(
                    "-c", "max_connections=300",
                    "-c", "max_locks_per_transaction=1024",
                    "-c", "shared_buffers=256MB"
                )
                .Build();

            await _dbContainer.StartAsync();

            _connectionString = _dbContainer.GetConnectionString();

            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE EXTENSION IF NOT EXISTS unaccent;";
            await cmd.ExecuteNonQueryAsync();
        }

        [After(Class)]
        public static async Task CleanupClassAsync()
            => await _dbContainer.DisposeAsync();

        [Before(Test)]
        public async Task SetupAsync()
        {
            _currentSchema = "test_schema_" + Guid.NewGuid().ToString("N");

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"CREATE SCHEMA IF NOT EXISTS \"{_currentSchema}\";";
                await cmd.ExecuteNonQueryAsync();
            }

            var schemaConnectionString = $"{_connectionString};SearchPath={_currentSchema},public";

            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(schemaConnectionString, options =>
                {
                    options.UseNetTopologySuite();
                    options.MigrationsHistoryTable("__EFMigrationsHistory", _currentSchema);
                })
               .AddInterceptors(new SoftDeleteInterceptor())
               .LogTo(Console.WriteLine, LogLevel.Warning)
               .EnableSensitiveDataLogging()
               .EnableDetailedErrors()
               .Options;

            _contextMock = new AppDbContext(dbOptions);

            var createScript = _contextMock.Database.GenerateCreateScript();
            await _contextMock.Database.ExecuteSqlRawAsync(createScript);

            _loggerMock = new LoggerFactory().CreateLogger<InvoiceEmailWorker>();
            _pdfGeneratorFake = new FakeInvoicePdfGenerator();
            _smtpServiceFake = new FakeSmtpEmailService();

            _worker = new InvoiceEmailWorker(
                _contextMock,
                _pdfGeneratorFake,
                _smtpServiceFake,
                _loggerMock);
        }

        [After(Test)]
        public async Task CleanupAsync()
        {
            await _contextMock.DisposeAsync();

            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DROP SCHEMA IF EXISTS {_currentSchema} CASCADE;";
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task<(Company Company, Currency Currency, Invoice Invoice)> SeedInvoiceGraphAsync(string? invoiceNumber = null)
        {
            var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
            var ownerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"User_{uniqueSuffix}",
                NormalizedUserName = $"USER_{uniqueSuffix}",
                Email = $"user_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"USER_{uniqueSuffix}@TEST.PL",
                FirstName = "Jan",
                LastName = "Kowalski"
            };

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
                Name = $"Firma_{uniqueSuffix}",
                NIP = "1234567890",
                OwnerId = owner.Id
            };

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Anna",
                LastName = "Nowak",
                CompanyId = company.Id,
                OwnerId = owner.Id,
                IsPrimary = true
            };

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = $"D/{uniqueSuffix}",
                Value = 100000,
                Status = DealsStatusEnum.Complete,
                CloseDate = DateTime.UtcNow,
                CurrencyId = currency.Id,
                CompanyId = company.Id,
                OwnerId = owner.Id,
                ContactId = contact.Id
            };

            var invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = invoiceNumber ?? $"FV/{uniqueSuffix}",
                TotalAmount = 100000,
                PaidAmount = 0,
                IssueDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(14),
                CurrencyId = currency.Id,
                CompanyId = company.Id,
                DealId = deal.Id
            };

            var unit = new UnitOfMeasure { Id = Guid.NewGuid(), Name = "Sztuka", Symbol = "szt.", BaseMultiplier = 1, IsDeleted = false };
            var steelGrade = new SteelGrade { Id = Guid.NewGuid(), Name = "1.4301", Density = 7900, IsDeleted = false };
            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Rura Stalowa",
                SteelGradeId = steelGrade.Id,
                UnitId = unit.Id,
                CurrencyId = currency.Id,
                PricePerUnit = 20000,
                StockQuantity = 50,
                Category = ProductCategoryEnum.Other
            };

            var invoiceProduct = new InvoiceProducts
            {
                Id = Guid.NewGuid(),
                InvoiceId = invoice.Id,
                ProductId = product.Id,
                ProductName = product.Name,
                UnitSymbol = "szt.",
                Quantity = 5,
                UnitPrice = 20000
            };

            _contextMock.Users.Add(owner);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.Add(contact);
            _contextMock.Deals.Add(deal);
            _contextMock.UnitsOfMeasure.Add(unit);
            _contextMock.SteelGrades.Add(steelGrade);
            _contextMock.Products.Add(product);
            _contextMock.Invoices.Add(invoice);
            _contextMock.InvoiceProducts.Add(invoiceProduct);

            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            return (company, currency, invoice);
        }

        // ─── GenerateAndSendInvoiceEmailAsync ────────────────────────────────────────────────────────────

        [Test]
        public async Task GenerateAndSendInvoiceEmailAsync_WhenInvoiceDoesNotExist_AbortsWithoutGeneratingPdfOrSendingEmail()
        {
            // Arrange
            var nonExistentInvoiceId = Guid.NewGuid();

            // Act
            await _worker.GenerateAndSendInvoiceEmailAsync(
                nonExistentInvoiceId,
                "klient@test.pl",
                "Twoja Faktura",
                "<p>W załączniku faktura</p>",
                "pl");

            // Assert
            await Assert.That(_pdfGeneratorFake.GenerateCallCount).IsEqualTo(0);
            await Assert.That(_smtpServiceFake.SentEmails).IsEmpty();
        }

        [Test]
        public async Task GenerateAndSendInvoiceEmailAsync_WhenLanguageIsEn_GeneratesEnglishPdfAndSetsEnglishFilename()
        {
            // Arrange
            var (_, _, invoice) = await SeedInvoiceGraphAsync(invoiceNumber: "FV/2026/01");

            // Act
            await _worker.GenerateAndSendInvoiceEmailAsync(
                invoice.Id,
                "client@company.com",
                "Your Invoice",
                "<p>Invoice attached</p>",
                "en");

            // Assert
            await Assert.That(_pdfGeneratorFake.LastLanguage).IsEqualTo("en");
            await Assert.That(_smtpServiceFake.SentEmails).Count().IsEqualTo(1);

            var sent = _smtpServiceFake.SentEmails.First();
            await Assert.That(sent.Filename).IsEqualTo("Invoice_FV_2026_01.pdf");
            await Assert.That(sent.RecipientEmail).IsEqualTo("client@company.com");
            await Assert.That(sent.Subject).IsEqualTo("Your Invoice");
            await Assert.That(sent.HtmlBody).IsEqualTo("<p>Invoice attached</p>");
        }

        [Test]
        [Arguments("pl")]
        [Arguments("PL")]
        [Arguments("de")]
        [Arguments("")]
        [Arguments(null)]
        public async Task GenerateAndSendInvoiceEmailAsync_WhenLanguageIsNotEn_DefaultsToPolish(string? language)
        {
            // Arrange
            var (_, _, invoice) = await SeedInvoiceGraphAsync(invoiceNumber: "FV/2026/99");

            // Act
            await _worker.GenerateAndSendInvoiceEmailAsync(
                invoice.Id,
                "klient@test.pl",
                "Faktura VAT",
                "<p>Treść</p>",
                language!);

            // Assert
            await Assert.That(_pdfGeneratorFake.LastLanguage).IsEqualTo("pl");
            await Assert.That(_smtpServiceFake.SentEmails).Count().IsEqualTo(1);
            await Assert.That(_smtpServiceFake.SentEmails.First().Filename).IsEqualTo("Faktura_FV_2026_99.pdf");
        }

        [Test]
        public async Task GenerateAndSendInvoiceEmailAsync_WhenInvoiceNumberContainsSlashesAndBackslashes_SanitizesFilenameCorrectly()
        {
            // Arrange
            var (_, _, invoice) = await SeedInvoiceGraphAsync(invoiceNumber: @"FV/2026\05/123\A");

            // Act
            await _worker.GenerateAndSendInvoiceEmailAsync(
                invoice.Id,
                "klient@test.pl",
                "Faktura VAT",
                "<p>Treść</p>",
                "pl");

            // Assert
            await Assert.That(_smtpServiceFake.SentEmails).Count().IsEqualTo(1);
            await Assert.That(_smtpServiceFake.SentEmails.First().Filename).IsEqualTo("Faktura_FV_2026_05_123_A.pdf");
        }

        [Test]
        public async Task GenerateAndSendInvoiceEmailAsync_WhenInvoiceExists_IncludesAllNavigationPropertiesForPdf()
        {
            // Arrange
            var (_, _, invoice) = await SeedInvoiceGraphAsync(invoiceNumber: "FV/NAV/001");

            // Act
            await _worker.GenerateAndSendInvoiceEmailAsync(
                invoice.Id,
                "klient@test.pl",
                "Temat",
                "Treść",
                "pl");

            // Assert
            var passedInvoice = _pdfGeneratorFake.LastInvoice;
            await Assert.That(passedInvoice).IsNotNull();
            await Assert.That(passedInvoice!.Currency).IsNotNull();
            await Assert.That(passedInvoice.Company).IsNotNull();
            await Assert.That(passedInvoice.InvoiceProducts).IsNotNull();
            await Assert.That(passedInvoice.InvoiceProducts).Count().IsEqualTo(1);
        }

        [Test]
        public async Task GenerateAndSendInvoiceEmailAsync_PassesGeneratedBytesDirectlyToSmtpAttachment()
        {
            // Arrange
            var expectedBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 };
            _pdfGeneratorFake.FakePdfBytes = expectedBytes;

            var (_, _, invoice) = await SeedInvoiceGraphAsync(invoiceNumber: "FV/BYTES/01");

            // Act
            await _worker.GenerateAndSendInvoiceEmailAsync(
                invoice.Id,
                "klient@test.pl",
                "Faktura",
                "Treść",
                "pl");

            // Assert
            var sent = _smtpServiceFake.SentEmails.First();
            await Assert.That(sent.AttachmentBytes).IsEqualTo(expectedBytes);
        }
    }





}
