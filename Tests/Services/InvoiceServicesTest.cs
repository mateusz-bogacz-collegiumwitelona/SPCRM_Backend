using Domain.Constants;
using Domain.Enum;
using Domain.Exceptions.Exception;
using Domain.Models;
using Infrastructure;
using Infrastructure.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Services.Command.Company;
using Services.Command.Invoice;
using Services.Command.List;
using Services.Services;
using Testcontainers.PostgreSql;

namespace Tests.Services
{
    public class InvoiceServicesTest
    {
        protected AppDbContext _contextMock = null!;

        private static PostgreSqlContainer _dbContainer = null!;
        private static string _connectionString = null!;

        protected InvoiceService _invoiceServicesMock = null!;
        protected ILogger<InvoiceService> _loggerMock = null!;

        private string _currentSchema = null!;

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

            _loggerMock = new LoggerFactory().CreateLogger<InvoiceService>();
            _invoiceServicesMock = new InvoiceService(_contextMock, _loggerMock);
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

        private async Task<(Company Company, ApplicationUser Owner, Currency Currency, Deal Deal, Invoice Invoice)> SeedInvoiceGraphAsync(
             long totalAmount = 100000,
             long paidAmount = 0,
             DateTime? dueDate = null,
             string? invoiceNumber = null)
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
                Value = totalAmount,
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
                TotalAmount = totalAmount,
                PaidAmount = paidAmount,
                IssueDate = DateTime.UtcNow,
                DueDate = dueDate ?? DateTime.UtcNow.AddDays(14),
                CurrencyId = currency.Id,
                CompanyId = company.Id,
                DealId = deal.Id
            };

            _contextMock.Users.Add(owner);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.Add(contact);
            _contextMock.Deals.Add(deal);
            _contextMock.Invoices.Add(invoice);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            return (company, owner, currency, deal, invoice);
        }

        private async Task<Product> SeedProductAsync(Currency currency)
        {
            var unit = new UnitOfMeasure { Id = Guid.NewGuid(), Name = "Sztuka", Symbol = "szt.", BaseMultiplier = 1, IsDeleted = false };
            var steelGrade = new SteelGrade { Id = Guid.NewGuid(), Name = "1.4301", Density = 7900, IsDeleted = false };
            _contextMock.UnitsOfMeasure.Add(unit);
            _contextMock.SteelGrades.Add(steelGrade);

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Produkt Bazowy",
                SteelGradeId = steelGrade.Id,
                UnitId = unit.Id,
                CurrencyId = currency.Id,
                PricePerUnit = 10000,
                StockQuantity = 50,
                Category = ProductCategoryEnum.Other
            };

            _contextMock.Products.Add(product);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            return product;
        }

        // ─── GetCompanyDebtSummaryAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetCompanyDebtSummaryAsync_GroupsByCurrencyAndCalculatesCorrectAmount()
        {
            // Arrange 
            var (company, _, currency, deal, _) = await SeedInvoiceGraphAsync(
                totalAmount: 50000,
                paidAmount: 50000);

            var currencyEur = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "EUR",
                Code = "EUR",
                DecimalPlaces = 2
            };

            var invoice1 = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = "F/1",
                CompanyId = company.Id,
                CurrencyId = currency.Id,
                TotalAmount = 10000000,
                PaidAmount = 5000000,
                DueDate = DateTime.UtcNow.AddDays(-5),
                DealId = deal.Id
            };

            var invoice2 = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = "F/2",
                CompanyId = company.Id,
                CurrencyId = currency.Id,
                TotalAmount = 2000000,
                PaidAmount = 2000000,
                DueDate = DateTime.UtcNow.AddDays(-5),
                DealId = deal.Id
            };

            var invoice3 = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = "F/3",
                CompanyId = company.Id,
                CurrencyId = currency.Id,
                TotalAmount = 3000000,
                PaidAmount = 0,
                DueDate = DateTime.UtcNow.AddDays(-5),
                DealId = deal.Id
            };

            var invoice4 = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = "F/4",
                CompanyId = company.Id,
                CurrencyId = currencyEur.Id,
                TotalAmount = 2000000,
                PaidAmount = 995000,
                DueDate = DateTime.UtcNow.AddDays(-5),
                DealId = deal.Id
            };

            _contextMock.Currencies.Add(currencyEur);
            _contextMock.Invoices.AddRange(invoice1, invoice2, invoice3, invoice4);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _invoiceServicesMock.GetCompanyDebtSummaryAsync(company.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!).Count().IsEqualTo(2);

            var plnSummary = result.Data!.First(s => s.CurrencyCode == "PLN");
            await Assert.That(plnSummary.TotalAmount).IsEqualTo(8000000L);
            await Assert.That(plnSummary.DecimalPlace).IsEqualTo(2);

            var eurSummary = result.Data!.First(s => s.CurrencyCode == "EUR");
            await Assert.That(eurSummary.TotalAmount).IsEqualTo(1005000L);
        }

        [Test]
        public async Task GetCompanyDebtSummaryAsync_WhenNoDebts_ReturnsEmptyList()
        {
            var (company, owner, currency, deal, _) = await SeedInvoiceGraphAsync(
                totalAmount: 50000,
                paidAmount: 50000);

            var invoice1 = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = "F/1",
                CompanyId = company.Id,
                CurrencyId = currency.Id,
                TotalAmount = 50000,
                PaidAmount = 50000,
                DueDate = DateTime.UtcNow.AddDays(-5),
                DealId = deal.Id
            };

            _contextMock.Invoices.Add(invoice1);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _invoiceServicesMock.GetCompanyDebtSummaryAsync(company.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!).IsEmpty();
        }

        [Test]
        public async Task GetCompanyDebtSummaryAsync_WhenCompanyDoesNotExist_Returns404NotFound()
        {
            // Arrange
            var nonExistentCompanyId = Guid.NewGuid();

            // Act
            var result = await _invoiceServicesMock.GetCompanyDebtSummaryAsync(nonExistentCompanyId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.CompanyNotFound);
            await Assert.That(result.Message).IsEqualTo("Company not found.");
        }

        [Test]
        [Arguments(-100, 500, 2, "PLN")]
        [Arguments(-500, -100, 2, "PLN")]
        [Arguments(0, 500, -1, "PLN")]
        [Arguments(0, 500, 2, "")]
        public async Task GetCompanyDebtSummaryAsync_WhenInvoiceDataIsCorrupted_ThrowsDataCorruptionException(
              long paidAmount,
              long totalAmount,
              int currencyDecimalPlaces,
              string currencyCode)
        {
            // Arrange 
            var (company, _, currency, deal, _) = await SeedInvoiceGraphAsync(
                totalAmount: 50000,
                paidAmount: 50000);

            if (currencyDecimalPlaces < 0 || string.IsNullOrWhiteSpace(currencyCode))
            {
                var dbCurrency = await _contextMock.Currencies.FindAsync(currency.Id);
                if (dbCurrency != null)
                {
                    dbCurrency.DecimalPlaces = currencyDecimalPlaces;
                    dbCurrency.Code = currencyCode;
                    await _contextMock.SaveChangesAsync();
                    _contextMock.ChangeTracker.Clear();
                }
            }

            var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];

            var corruptedInvoice = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = $"INV/ERR/{uniqueSuffix}",
                CompanyId = company.Id,
                CurrencyId = currency.Id,
                TotalAmount = totalAmount,
                PaidAmount = paidAmount,
                DueDate = DateTime.UtcNow.AddDays(5),
                DealId = deal.Id
            };

            _contextMock.Invoices.Add(corruptedInvoice);
            await _contextMock.SaveChangesAsync();

            // Act & Assert
            await Assert.That(async () => await _invoiceServicesMock.GetCompanyDebtSummaryAsync(company.Id))
                .Throws<DataCorruptionException>();
        }


        // ─── GetCompanyDebtSummaryAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetCompanyDebtsAsync_MapsDataAndCalculatesDaysOverdueCorrectly()
        {
            // Arrange
            var (company, owner, currency, deal, _) = await SeedInvoiceGraphAsync(
                totalAmount: 50000,
                paidAmount: 50000);

            var now = DateTime.UtcNow;

            var overdueInvoice = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = "INV/OVER",
                CompanyId = company.Id,
                CurrencyId = currency.Id,
                DealId = deal.Id,
                TotalAmount = 2000000,
                PaidAmount = 0,
                DueDate = now.AddDays(-10),
            };

            var futureInvoice = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = "INV/FUTURE",
                CompanyId = company.Id,
                CurrencyId = currency.Id,
                DealId = deal.Id,
                TotalAmount = 3000000,
                PaidAmount = 1000000,
                DueDate = now.AddDays(5)
            };

            var paidInvoice = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = "INV/PAID",
                CompanyId = company.Id,
                CurrencyId = currency.Id,
                DealId = deal.Id,
                TotalAmount = 5000000,
                PaidAmount = 5000000,
                DueDate = now.AddDays(-20)
            };

            _contextMock.Invoices.AddRange(overdueInvoice, futureInvoice, paidInvoice);
            await _contextMock.SaveChangesAsync();

            var command = new CompanyCommand
            {
                PageNumber = 1,
                PageSize = 10,
                CompanyId = company.Id
            };

            // Act
            var result = await _invoiceServicesMock.GetCompanyDebtsAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var items = result.Data!.Items;

            await Assert.That(items).Count().IsEqualTo(2);

            var firstMapped = items[0];
            await Assert.That(firstMapped.InvoiceNumber).IsEqualTo("INV/OVER");
            await Assert.That(firstMapped.AmountLeft).IsEqualTo(2000000m);
            await Assert.That(firstMapped.CurrencyCode).IsEqualTo("PLN");
            await Assert.That(firstMapped.DaysOverdue).IsGreaterThanOrEqualTo(9);

            var secondMapped = items[1];
            await Assert.That(secondMapped.InvoiceNumber).IsEqualTo("INV/FUTURE");
            await Assert.That(secondMapped.AmountLeft).IsEqualTo(2000000m);
            await Assert.That(secondMapped.DaysOverdue).IsEqualTo(0);
        }

        [Test]
        public async Task GetCompanyDebtsAsync_WhenCompanyDoesNotExist_Returns404NotFound()
        {
            // Arrange
            var command = new CompanyCommand
            {
                PageNumber = 1,
                PageSize = 10,
                CompanyId = Guid.NewGuid()
            };

            // Act
            var result = await _invoiceServicesMock.GetCompanyDebtsAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.CompanyNotFound);
            await Assert.That(result.Message).IsEqualTo("Company not found.");
        }

        [Test]
        public async Task GetCompanyDebtsAsync_WhenCompanyContainsCorruptedInvoices_ThrowsDataCorruptionException()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];

            var (company, _, currency, deal, _) = await SeedInvoiceGraphAsync(
                totalAmount: 50000,
                paidAmount: 50000);

            var invalidInvoice = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = $"INV/BAD/{uniqueSuffix}",
                CompanyId = company.Id,
                CurrencyId = currency.Id,
                DealId = deal.Id,
                TotalAmount = 20000,
                PaidAmount = -500,
                DueDate = DateTime.UtcNow.AddDays(2)
            };

            _contextMock.Invoices.Add(invalidInvoice);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            var command = new CompanyCommand
            {
                PageNumber = 1,
                PageSize = 10,
                CompanyId = company.Id
            };

            // Act & Assert
            await Assert.That(async () => await _invoiceServicesMock.GetCompanyDebtsAsync(command))
                .Throws<DataCorruptionException>();
        }


        // ─── GetInvoiceListAsync ───────────────────────────────────────────────────

        [Test]
        public async Task GetInvoiceListAsync_MapsAllFieldsAndCalculatesAmountsCorrectly()
        {
            var issueDate = DateTime.UtcNow.AddDays(-10);
            var dueDate = DateTime.UtcNow.AddDays(4);

            var (company, _, _, _, invoice) = await SeedInvoiceGraphAsync(
                totalAmount: 5000000,
                paidAmount: 2000000,
                dueDate: dueDate);

            invoice.IssueDate = issueDate;
            _contextMock.Invoices.Update(invoice);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            var command = new InvoiceListCommand
            {
                CompanyNip = company.NIP,
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _invoiceServicesMock.GetInvoiceListAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var items = result.Data!.Items;
            await Assert.That(items).Count().IsEqualTo(1);
            await Assert.That(result.Data.TotalCount).IsEqualTo(1);

            var item = items.First();
            await Assert.That(item.Id).IsEqualTo(invoice.Id);
            await Assert.That(item.InvoiceNumber).IsEqualTo(invoice.InvoiceNumber);
            await Assert.That(item.TotalAmount).IsEqualTo(5000000L);
            await Assert.That(item.PaidAmount).IsEqualTo(2000000L);
            await Assert.That(item.RemainingAmount).IsEqualTo(3000000L);
            await Assert.That(item.CompanyName).IsEqualTo(company.Name);
            await Assert.That(item.CompanyNip).IsEqualTo(company.NIP);
            await Assert.That(item.CurrencyCode).IsEqualTo("PLN");
            await Assert.That(item.DecimalPlaces).IsEqualTo(2);
            await Assert.That(item.IsOverDue).IsFalse();
        }

        [Test]
        public async Task GetInvoiceListAsync_AppliesPaginationCorrectly()
        {
            // Arrange
            var suffix = Guid.NewGuid().ToString("N")[..8];

            var (company, owner, currency, deal, _) = await SeedInvoiceGraphAsync(
                totalAmount: 50000,
                paidAmount: 20000);

            for (int i = 1; i <= 5; i++)
            {
                _contextMock.Invoices.Add(new Invoice
                {
                    Id = Guid.NewGuid(),
                    InvoiceNumber = $"FV/PAG/{suffix}/{i}",
                    CompanyId = company.Id,
                    CurrencyId = currency.Id,
                    TotalAmount = 1000000 * i,
                    PaidAmount = 0,
                    IssueDate = DateTime.UtcNow.AddDays(-i),
                    DueDate = DateTime.UtcNow.AddDays(10),
                    DealId = deal.Id
                });
            }
            await _contextMock.SaveChangesAsync();

            var command = new InvoiceListCommand
            {
                PageNumber = 2,
                PageSize = 2
            };

            // Act
            var result = await _invoiceServicesMock.GetInvoiceListAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.Items).Count().IsEqualTo(2);
            await Assert.That(result.Data.TotalCount).IsEqualTo(6);
            await Assert.That(result.Data.PageNumber).IsEqualTo(2);
        }

        [Test]
        public async Task GetInvoiceListAsync_SortsByTotalAmountAscendingAndDescending()
        {
            // Arrange
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var (company, owner, currency, deal, _) = await SeedInvoiceGraphAsync(
                totalAmount: 1000000,
                paidAmount: 20000);

            var invLow = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = $"FV/LOW/{suffix}",
                CompanyId = company.Id,
                CurrencyId = currency.Id,
                TotalAmount = 1000000,
                IssueDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(7),
                DealId = deal.Id
            };

            var invHigh = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = $"FV/HIGH/{suffix}",
                CompanyId = company.Id,
                CurrencyId = currency.Id,
                TotalAmount = 9000000,
                IssueDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(7),
                DealId = deal.Id
            };

            _contextMock.Invoices.AddRange(invLow, invHigh);
            await _contextMock.SaveChangesAsync();

            // Act
            var resultAsc = await _invoiceServicesMock.GetInvoiceListAsync(new InvoiceListCommand
            {
                SortBy = "totalamount",
                SortDescending = false,
                PageNumber = 1,
                PageSize = 10
            });

            var resultDesc = await _invoiceServicesMock.GetInvoiceListAsync(new InvoiceListCommand
            {
                SortBy = "totalamount",
                SortDescending = true,
                PageNumber = 1,
                PageSize = 10
            });

            // Assert
            await Assert.That(resultAsc.Data!.Items.First().TotalAmount).IsEqualTo(1000000L);
            await Assert.That(resultDesc.Data!.Items.First().TotalAmount).IsEqualTo(9000000L);
        }

        [Test]
        public async Task GetInvoiceListAsync_FiltersByTotalAmountRangeAndIssueDates()
        {
            // Arrange
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var (company, owner, currency, deal, _) = await SeedInvoiceGraphAsync(
                    totalAmount: 50000,
                    paidAmount: 20000);

            var now = DateTime.UtcNow;

            var matchingInvoice = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = $"FV/MATCH/{suffix}",
                CompanyId = company.Id,
                CurrencyId = currency.Id,
                TotalAmount = 5000000,
                IssueDate = now.AddDays(-5),
                DueDate = now.AddDays(10),
                DealId = deal.Id
            };

            var nonMatchingAmount = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = $"FV/OUT_AMOUNT/{suffix}",
                CompanyId = company.Id,
                CurrencyId = currency.Id,
                TotalAmount = 1000000,
                IssueDate = now.AddDays(-5),
                DueDate = now.AddDays(10),
                DealId = deal.Id
            };

            var nonMatchingDate = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = $"FV/OUT_DATE/{suffix}",
                CompanyId = company.Id,
                CurrencyId = currency.Id,
                TotalAmount = 5000000,
                IssueDate = now.AddDays(-30),
                DueDate = now.AddDays(10),
                DealId = deal.Id
            };

            _contextMock.Invoices.AddRange(matchingInvoice, nonMatchingAmount, nonMatchingDate);
            await _contextMock.SaveChangesAsync();

            var command = new InvoiceListCommand
            {
                TotalAmountFrom = 4000000,
                TotalAmountTo = 6000000,
                IssueDateFrom = now.AddDays(-10),
                IssueDateTo = now,
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _invoiceServicesMock.GetInvoiceListAsync(command);

            // Assert
            await Assert.That(result.Data!.Items).Count().IsEqualTo(1);
            await Assert.That(result.Data.Items.First().InvoiceNumber).IsEqualTo(matchingInvoice.InvoiceNumber);
        }

        [Test]
        public async Task GetInvoiceListAsync_FiltersByIsOverDueCorrectly()
        {
            // Arrange
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var (company, owner, currency, deal, _) = await SeedInvoiceGraphAsync(
                totalAmount: 50000,
                paidAmount: 20000);
            var now = DateTime.UtcNow;

            var overdueInvoice = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = $"FV/OVER/{suffix}",
                CompanyId = company.Id,
                CurrencyId = currency.Id,
                TotalAmount = 3000000,
                PaidAmount = 1000000,
                IssueDate = now.AddDays(-20),
                DueDate = now.AddDays(-5),
                DealId = deal.Id
            };

            var futureInvoice = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = $"FV/FUTURE/{suffix}",
                CompanyId = company.Id,
                CurrencyId = currency.Id,
                TotalAmount = 3000000,
                PaidAmount = 1000000,
                IssueDate = now.AddDays(-5),
                DueDate = now.AddDays(10),
                DealId = deal.Id
            };

            var fullyPaidOverdueDate = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = $"FV/PAID/{suffix}",
                CompanyId = company.Id,
                CurrencyId = currency.Id,
                TotalAmount = 3000000,
                PaidAmount = 3000000,
                IssueDate = now.AddDays(-20),
                DueDate = now.AddDays(-5),
                DealId = deal.Id
            };

            _contextMock.Invoices.AddRange(overdueInvoice, futureInvoice, fullyPaidOverdueDate);
            await _contextMock.SaveChangesAsync();

            var command = new InvoiceListCommand
            {
                IsOverDue = true,
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _invoiceServicesMock.GetInvoiceListAsync(command);

            // Assert
            await Assert.That(result.Data!.Items).Count().IsEqualTo(1);
            await Assert.That(result.Data.Items.First().InvoiceNumber).IsEqualTo(overdueInvoice.InvoiceNumber);
            await Assert.That(result.Data.Items.First().IsOverDue).IsTrue();
        }

        [Test]
        public async Task GetInvoiceListAsync_SearchesByCompanyNameAndNipUsingUnaccent()
        {
            // Arrange
            var (company, _, _, _, invoice) = await SeedInvoiceGraphAsync(
                totalAmount: 50000,
                paidAmount: 20000);

            var dbCompany = await _contextMock.Companies.FindAsync(company.Id);
            dbCompany!.Name = "Hurtownia Żelazo Stal";
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            // Act 
            var result = await _invoiceServicesMock.GetInvoiceListAsync(new InvoiceListCommand
            {
                SearchTerm = "zelazo",
                PageNumber = 1,
                PageSize = 10
            });

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var items = result.Data!.Items;
            await Assert.That(items).Count().IsEqualTo(1);
            await Assert.That(items.First().InvoiceNumber).IsEqualTo(invoice.InvoiceNumber);
            await Assert.That(items.First().CompanyName).IsEqualTo("Hurtownia Żelazo Stal");
        }

        // ─── GetInvoiceDetailAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetInvoiceDetailAsync_WhenInvoiceExistsWithDeal_ReturnsFullDetails()
        {
            // Arrange
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var owner = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"U_{suffix}",
                NormalizedUserName = $"U_{suffix}".ToUpper(),
                Email = $"u_{suffix}@test.pl",
                NormalizedEmail = $"U_{suffix}@TEST.PL",
                FirstName = "Piotr",
                LastName = "Kowalski"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Hurtownia Stali {suffix}",
                NIP = "5554443322",
                OwnerId = owner.Id,
                Owner = owner
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Polski Złoty",
                Code = "PLN",
                DecimalPlaces = 2
            };

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Marek",
                LastName = "Nowak",
                CompanyId = company.Id,
                Company = company,
                OwnerId = owner.Id,
                Owner = owner,
                IsPrimary = true
            };

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = $"D/2026/09/18/{suffix}",
                Value = 10000000,
                Status = DealsStatusEnum.InProgress,
                CloseDate = DateTime.UtcNow.AddDays(7),
                CompanyId = company.Id,
                Company = company,
                OwnerId = owner.Id,
                Owner = owner,
                CurrencyId = currency.Id,
                Currency = currency,
                ContactId = contact.Id,
                Contact = contact
            };

            var issueDate = DateTime.UtcNow.AddDays(-2);
            var dueDate = DateTime.UtcNow.AddDays(12);
            var paymentDate = DateTime.UtcNow.AddDays(-1);

            var invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = $"FV/DETAIL/{suffix}",
                CompanyId = company.Id,
                Company = company,
                CurrencyId = currency.Id,
                Currency = currency,
                DealId = deal.Id,
                Deal = deal,
                TotalAmount = 10000000,
                PaidAmount = 10000000,
                IssueDate = issueDate,
                DueDate = dueDate,
                PaymentDate = paymentDate
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.Currencies.Add(currency);
            _contextMock.Contacts.Add(contact);
            _contextMock.Deals.Add(deal);
            _contextMock.Invoices.Add(invoice);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _invoiceServicesMock.GetInvoiceDetailAsync(invoice.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();

            var data = result.Data!;
            await Assert.That(data.InvoiceId).IsEqualTo(invoice.Id);
            await Assert.That(data.InvoiceNumber).IsEqualTo($"FV/DETAIL/{suffix}");
            await Assert.That(data.CompanyId).IsEqualTo(company.Id);
            await Assert.That(data.CompanyName).IsEqualTo(company.Name);
            await Assert.That(data.CompanyNip).IsEqualTo(company.NIP);
            await Assert.That(data.DealId).IsEqualTo(deal.Id);
            await Assert.That(data.DealName).IsEqualTo(deal.Name);
            await Assert.That(data.PaymentDate).IsNotNull();
        }

        [Test]
        public async Task GetInvoiceDetailAsync_WhenInvoiceDoesNotExist_Returns404NotFound()
        {
            // Arrange
            var nonExistentInvoiceId = Guid.NewGuid();

            // Act
            var result = await _invoiceServicesMock.GetInvoiceDetailAsync(nonExistentInvoiceId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvoiceNotFound);
            await Assert.That(result.Message).IsEqualTo("Invoice not found.");
        }

        // ─── GetInvoiceProductAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetInvoiceProductAsync_MapsAllFieldsCorrectly()
        {
            // Arrange
            var (company, _, currency, deal, invoice) = await SeedInvoiceGraphAsync(
                totalAmount: 100000,
                paidAmount: 100000);

            var realProduct = await SeedProductAsync(currency);

            var invoiceProduct = new InvoiceProducts
            {
                Id = Guid.NewGuid(),
                InvoiceId = invoice.Id,
                ProductId = realProduct.Id,
                ProductName = "Rura Nierdzewna 42.4x2",
                SteelGrade = "1.4301",
                UnitSymbol = "m",
                Quantity = 5,
                UnitPrice = 20000,
            };

            _contextMock.InvoiceProducts.Add(invoiceProduct);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            var command = new SimpleListCommand
            {
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _invoiceServicesMock.GetInvoiceProductAsync(invoice.Id, command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var items = result.Data!.Items;
            await Assert.That(items).Count().IsEqualTo(1);

            var mapped = items.First();
            await Assert.That(mapped.InvoiceProductId).IsEqualTo(invoiceProduct.Id);
            await Assert.That(mapped.ProductName).IsEqualTo("Rura Nierdzewna 42.4x2");
            await Assert.That(mapped.SteelGrade).IsEqualTo("1.4301");
            await Assert.That(mapped.UnitSymbol).IsEqualTo("m");
            await Assert.That(mapped.Quantity).IsEqualTo(5);
            await Assert.That(mapped.UnitPrice).IsEqualTo(20000L);
            await Assert.That(mapped.TotalPrice).IsEqualTo(100000L);
        }

        [Test]
        public async Task GetInvoiceProductAsync_ReturnsOnlyProductsForSpecificInvoice()
        {
            // Arrange
            var (company, _, currency, deal, invoiceTarget) = await SeedInvoiceGraphAsync(
                totalAmount: 50000,
                paidAmount: 50000);

            var realProduct = await SeedProductAsync(currency);

            var invoiceOther = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = "FV/OTHER/001",
                CompanyId = company.Id,
                CurrencyId = currency.Id,
                DealId = deal.Id,
                TotalAmount = 50000,
                PaidAmount = 50000,
                IssueDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(14)
            };
            _contextMock.Invoices.Add(invoiceOther);

            var productTarget = new InvoiceProducts
            {
                Id = Guid.NewGuid(),
                InvoiceId = invoiceTarget.Id,
                ProductId = realProduct.Id,
                ProductName = "Docelowy Produkt",
                UnitSymbol = "szt.",
                Quantity = 1,
                UnitPrice = 50000
            };

            var productOther = new InvoiceProducts
            {
                Id = Guid.NewGuid(),
                InvoiceId = invoiceOther.Id,
                ProductId = realProduct.Id,
                ProductName = "Inny Produkt",
                UnitSymbol = "szt.",
                Quantity = 2,
                UnitPrice = 25000
            };

            _contextMock.InvoiceProducts.AddRange(productTarget, productOther);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            var command = new SimpleListCommand { PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _invoiceServicesMock.GetInvoiceProductAsync(invoiceTarget.Id, command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var items = result.Data!.Items;
            await Assert.That(items).Count().IsEqualTo(1);
            await Assert.That(items.First().InvoiceProductId).IsEqualTo(productTarget.Id);
            await Assert.That(items.First().ProductName).IsEqualTo("Docelowy Produkt");
        }

        [Test]
        public async Task GetInvoiceProductAsync_WhenNoProductsFoundOrInvalidInvoiceId_ReturnsEmptyListWithSuccess()
        {
            // Arrange
            var randomInvoiceId = Guid.NewGuid();
            var command = new SimpleListCommand { PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _invoiceServicesMock.GetInvoiceProductAsync(randomInvoiceId, command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.Items).IsEmpty();
            await Assert.That(result.Data.TotalCount).IsEqualTo(0);
        }

        [Test]
        public async Task GetInvoiceProductAsync_AppliesPaginationCorrectly()
        {
            // Arrange
            var (company, _, currency, deal, invoice) = await SeedInvoiceGraphAsync(
                totalAmount: 300000,
                paidAmount: 300000);

            var unit = new UnitOfMeasure { Id = Guid.NewGuid(), Name = "Sztuka", Symbol = "szt.", BaseMultiplier = 1, IsDeleted = false };
            var steelGrade = new SteelGrade { Id = Guid.NewGuid(), Name = "1.4301", Density = 7900, IsDeleted = false };
            _contextMock.UnitsOfMeasure.Add(unit);
            _contextMock.SteelGrades.Add(steelGrade);

            var realProduct = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Produkt Bazowy",
                SteelGradeId = steelGrade.Id,
                UnitId = unit.Id,
                CurrencyId = currency.Id,
                PricePerUnit = 100000,
                StockQuantity = 50,
                Category = ProductCategoryEnum.Other
            };
            _contextMock.Products.Add(realProduct);
            await _contextMock.SaveChangesAsync();

            var products = new List<InvoiceProducts>
            {
                new() { Id = Guid.NewGuid(), InvoiceId = invoice.Id, ProductId = realProduct.Id, ProductName = "Produkt 1", UnitSymbol = "szt.", Quantity = 1, UnitPrice = 100000 },
                new() { Id = Guid.NewGuid(), InvoiceId = invoice.Id, ProductId = realProduct.Id, ProductName = "Produkt 2", UnitSymbol = "szt.", Quantity = 1, UnitPrice = 100000 },
                new() { Id = Guid.NewGuid(), InvoiceId = invoice.Id, ProductId = realProduct.Id, ProductName = "Produkt 3", UnitSymbol = "szt.", Quantity = 1, UnitPrice = 100000 },
            };

            _contextMock.InvoiceProducts.AddRange(products);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            var command = new SimpleListCommand
            {
                PageNumber = 1,
                PageSize = 2
            };

            // Act
            var result = await _invoiceServicesMock.GetInvoiceProductAsync(invoice.Id, command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.Items).Count().IsEqualTo(2);
            await Assert.That(result.Data.TotalCount).IsEqualTo(3);
            await Assert.That(result.Data.TotalPages).IsEqualTo(2);
        }

        [Test]
        public async Task GetInvoiceProductAsync_SearchesByProductNameAndSteelGradeUsingUnaccent()
        {
            // Arrange
            var (company, _, currency, deal, invoice) = await SeedInvoiceGraphAsync(
                totalAmount: 200000,
                paidAmount: 200000);

            var realProduct = await SeedProductAsync(currency);

            var matchedProduct = new InvoiceProducts
            {
                Id = Guid.NewGuid(),
                InvoiceId = invoice.Id,
                ProductId = realProduct.Id,
                ProductName = "Płaskownik Żelazny",
                SteelGrade = "Żaroodporna",
                UnitSymbol = "mb",
                Quantity = 10,
                UnitPrice = 10000
            };

            var otherProduct = new InvoiceProducts
            {
                Id = Guid.NewGuid(),
                InvoiceId = invoice.Id,
                ProductId = realProduct.Id,
                ProductName = "Rura Miedziana",
                SteelGrade = "Miedź",
                UnitSymbol = "szt.",
                Quantity = 2,
                UnitPrice = 50000
            };

            _contextMock.InvoiceProducts.AddRange(matchedProduct, otherProduct);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            var command = new SimpleListCommand
            {
                SearchTerm = "plaskownik",
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _invoiceServicesMock.GetInvoiceProductAsync(invoice.Id, command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var items = result.Data!.Items;
            await Assert.That(items).Count().IsEqualTo(1);
            await Assert.That(items.First().InvoiceProductId).IsEqualTo(matchedProduct.Id);
            await Assert.That(items.First().ProductName).IsEqualTo("Płaskownik Żelazny");
        }
    }
}
