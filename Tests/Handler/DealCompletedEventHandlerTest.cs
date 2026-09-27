using Domain.Constants;
using Domain.Enum;
using Domain.Events;
using Domain.Models;
using Infrastructure;
using Infrastructure.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Services.Handlers;
using Testcontainers.PostgreSql;
using Tests.Fakes;

namespace Tests.Handler
{
    public class DealCompletedEventHandlerTest
    {
        private AppDbContext _contextMock = null!;
        private static PostgreSqlContainer _dbContainer = null!;
        private static string _connectionString = null!;
        private string _currentSchema = null!;

        private FakeEmailSender _emailSenderMock = null!;
        private ILogger<DealCompletedEventHandler> _loggerMock = null!;
        private DealCompletedEventHandler _handler = null!;

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
                cmd.CommandText = $"CREATE SCHEMA IF NOT EXISTS {_currentSchema};";
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

            _loggerMock = new LoggerFactory().CreateLogger<DealCompletedEventHandler>();
            _emailSenderMock = new FakeEmailSender();

            _handler = new DealCompletedEventHandler(
                _contextMock,
                _emailSenderMock,
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


        private async Task<(Company Company, Currency Currency, Deal Deal, Product ProductA, Product ProductB)> SeedDealWithProductsGraphAsync()
        {
            var unique = Guid.NewGuid().ToString("N")[..8];
            var userId = Guid.NewGuid();

            var user = new ApplicationUser
            {
                Id = userId,
                UserName = $"User_{unique}",
                NormalizedUserName = $"USER_{unique}",
                Email = $"user_{unique}@test.com",
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
                Name = $"Stal-Bud_{unique}",
                NIP = "1234567890",
                OwnerId = userId,
                Owner = user
            };

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Piotr",
                LastName = "Nowak",
                IsPrimary = true,
                CompanyId = company.Id,
                OwnerId = userId,
                Owner = user
            };

            var contactDetail = new ContactDetail
            {
                Id = Guid.NewGuid(),
                ContactId = contact.Id,
                Value = "piotr.nowak@stalbud.pl",
                Type = ContactDetailTypeEnum.EMAIL,
                IsPrimary = true
            };

            var unit = new UnitOfMeasure { Id = Guid.NewGuid(), Name = "Sztuka", Symbol = "szt.", BaseMultiplier = 1 };
            var steelGrade = new SteelGrade { Id = Guid.NewGuid(), Name = "1.4301", Density = 7900 };

            var productA = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Rura Nierdzewna",
                SteelGradeId = steelGrade.Id,
                SteelGrade = steelGrade,
                UnitId = unit.Id,
                Unit = unit,
                CurrencyId = currency.Id,
                PricePerUnit = 25000,
                StockQuantity = 50,
                Category = ProductCategoryEnum.Pipe
            };

            var productB = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Blacha Kwasoodporna",
                SteelGradeId = steelGrade.Id,
                SteelGrade = steelGrade,
                UnitId = unit.Id,
                Unit = unit,
                CurrencyId = currency.Id,
                PricePerUnit = 40000,
                StockQuantity = 30,
                Category = ProductCategoryEnum.Sheet
            };

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = $"D/2026/09/{unique}",
                Value = (2L * 25000) + (3L * 40000), 
                Status = DealsStatusEnum.Complete,
                CloseDate = DateTime.UtcNow,
                CompanyId = company.Id,
                Company = company,
                CurrencyId = currency.Id,
                Currency = currency,
                OwnerId = userId,
                Owner = user,
                ContactId = contact.Id,
                Contact = contact
            };

            var dealProductA = new DealProduct
            {
                Id = Guid.NewGuid(),
                DealId = deal.Id,
                ProductId = productA.Id,
                Product = productA,
                Quantity = 2,
                UnitPrice = 25000
            };

            var dealProductB = new DealProduct
            {
                Id = Guid.NewGuid(),
                DealId = deal.Id,
                ProductId = productB.Id,
                Product = productB,
                Quantity = 3,
                UnitPrice = 40000
            };

            _contextMock.Users.Add(user);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.Add(contact);
            _contextMock.ContactDetails.Add(contactDetail);
            _contextMock.UnitsOfMeasure.Add(unit);
            _contextMock.SteelGrades.Add(steelGrade);
            _contextMock.Products.AddRange(productA, productB);
            _contextMock.Deals.Add(deal);
            _contextMock.DealProducts.AddRange(dealProductA, dealProductB);

            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            return (company, currency, deal, productA, productB);
        }

        // ─── Handle ────────────────────────────────────────────────────────────

        [Test]
        public async Task Handle_WhenDealNotFound_LogsErrorAndAbortsWithoutCreatingInvoiceOrSendingEmail()
        {
            // Arrange
            var nonExistentDealId = Guid.NewGuid();
            var notification = new DealCompletedEvent(nonExistentDealId, "klient@test.pl", "pl");

            // Act
            await _handler.Handle(notification, CancellationToken.None);

            // Assert
            var invoicesCount = await _contextMock.Invoices.CountAsync();
            await Assert.That(invoicesCount).IsEqualTo(0);
            await Assert.That(_emailSenderMock.SentInvoiceEmails).IsEmpty();
        }

        [Test]
        public async Task Handle_WhenDealExists_CreatesInvoiceWithProductsAndSendsEmail()
        {
            // Arrange
            var (company, currency, deal, productA, productB) = await SeedDealWithProductsGraphAsync();
            var recipientEmail = "odbiorca@faktury.pl";
            var notification = new DealCompletedEvent(deal.Id, recipientEmail, "en");

            var expectedTotalAmount = (2L * 25000) + (3L * 40000); // 170000
            var expectedGrossAmount = expectedTotalAmount / BusinessConstants.CurrencyScaleFactor;

            // Act
            await _handler.Handle(notification, CancellationToken.None);

            // Assert
            var invoice = await _contextMock.Invoices
                .Include(i => i.InvoiceProducts)
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.DealId == deal.Id);

            await Assert.That(invoice).IsNotNull();
            await Assert.That(invoice!.CompanyId).IsEqualTo(company.Id);
            await Assert.That(invoice.CurrencyId).IsEqualTo(currency.Id);
            await Assert.That(invoice.TotalAmount).IsEqualTo(expectedTotalAmount);
            await Assert.That(invoice.PaidAmount).IsEqualTo(0L);
            await Assert.That(invoice.DueDate).IsGreaterThan(DateTime.UtcNow.AddDays(13));
            await Assert.That(invoice.InvoiceNumber).IsNotNull();

            await Assert.That(invoice.InvoiceProducts.Count).IsEqualTo(2);

            var invProdA = invoice.InvoiceProducts.First(p => p.ProductId == productA.Id);
            await Assert.That(invProdA.ProductName).IsEqualTo("Rura Nierdzewna");
            await Assert.That(invProdA.SteelGrade).IsEqualTo("1.4301");
            await Assert.That(invProdA.UnitSymbol).IsEqualTo("szt.");
            await Assert.That(invProdA.Quantity).IsEqualTo(2);
            await Assert.That(invProdA.UnitPrice).IsEqualTo(25000L);

            var invProdB = invoice.InvoiceProducts.First(p => p.ProductId == productB.Id);
            await Assert.That(invProdB.ProductName).IsEqualTo("Blacha Kwasoodporna");
            await Assert.That(invProdB.Quantity).IsEqualTo(3);
            await Assert.That(invProdB.UnitPrice).IsEqualTo(40000L);

            await Assert.That(_emailSenderMock.SentInvoiceEmails.Count).IsEqualTo(1);

            var sentMail = _emailSenderMock.LasInvoiceEmailDomain!;
            await Assert.That(sentMail.InvoiceId).IsEqualTo(invoice.Id);
            await Assert.That(sentMail.InvoiceNumber).IsEqualTo(invoice.InvoiceNumber);
            await Assert.That(sentMail.RecipientEmail).IsEqualTo(recipientEmail);
            await Assert.That(sentMail.RecipientName).IsEqualTo(company.Name);
            await Assert.That(sentMail.TotalGrossAmount).IsEqualTo(expectedGrossAmount);
            await Assert.That(sentMail.CurrencyCode).IsEqualTo("PLN");
            await Assert.That(sentMail.Language).IsEqualTo("en");
        }

        [Test]
        public async Task Handle_WhenInvoicesAlreadyExistInCurrentMonth_IncrementsInvoiceNumberCounter()
        {
            // Arrange
            var (company, currency, deal, _, _) = await SeedDealWithProductsGraphAsync();

            var now = DateTime.UtcNow;
            var year = now.Year;
            var month = now.Month;

            var existingInvoice = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = $"FV/{year:0000}/{month:00}/0001",
                IssueDate = now,
                DueDate = now.AddDays(14),
                TotalAmount = 50000,
                PaidAmount = 0,
                CompanyId = company.Id,
                CurrencyId = currency.Id,
                DealId = deal.Id
            };
            _contextMock.Invoices.Add(existingInvoice);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            var notification = new DealCompletedEvent(deal.Id, "klient@test.pl", "pl");

            // Act
            await _handler.Handle(notification, CancellationToken.None);

            // Assert
            var newInvoice = await _contextMock.Invoices
                .AsNoTracking()
                .Where(i => i.Id != existingInvoice.Id)
                .FirstOrDefaultAsync();

            await Assert.That(newInvoice).IsNotNull();
            await Assert.That(newInvoice!.InvoiceNumber).IsEqualTo($"FV/{year:0000}/{month:00}/0002");
        }

        [Test]
        public async Task Handle_WhenRecipientEmailIsNull_CreatesInvoiceWithoutSendingEmail()
        {
            // Arrange
            var (_, _, deal, _, _) = await SeedDealWithProductsGraphAsync();
            var notification = new DealCompletedEvent(deal.Id, null, "pl");

            // Act
            await _handler.Handle(notification, CancellationToken.None);

            // Assert
            var invoiceCreated = await _contextMock.Invoices.AnyAsync(i => i.DealId == deal.Id);
            await Assert.That(invoiceCreated).IsTrue();
            await Assert.That(_emailSenderMock.SentInvoiceEmails).IsEmpty();
        }

        [Test]
        [Arguments("")]
        [Arguments("   ")]
        public async Task Handle_WhenRecipientEmailIsWhitespace_CreatesInvoiceWithoutSendingEmail(string emptyEmail)
        {
            // Arrange
            var (_, _, deal, _, _) = await SeedDealWithProductsGraphAsync();
            var notification = new DealCompletedEvent(deal.Id, emptyEmail, "pl");

            // Act
            await _handler.Handle(notification, CancellationToken.None);

            // Assert
            var invoiceCreated = await _contextMock.Invoices.AnyAsync(i => i.DealId == deal.Id);
            await Assert.That(invoiceCreated).IsTrue();
            await Assert.That(_emailSenderMock.SentInvoiceEmails).IsEmpty();
        }

        [Test]
        public async Task Handle_WhenLanguageIsNull_DefaultsToPolishLanguageInEmailPayload()
        {
            // Arrange
            var (_, _, deal, _, _) = await SeedDealWithProductsGraphAsync();
            var notification = new DealCompletedEvent(deal.Id, "klient@test.pl", null);

            // Act
            await _handler.Handle(notification, CancellationToken.None);

            // Assert
            await Assert.That(_emailSenderMock.SentInvoiceEmails.Count).IsEqualTo(1);
            await Assert.That(_emailSenderMock.LasInvoiceEmailDomain!.Language).IsEqualTo("pl");
        }
    }
}
