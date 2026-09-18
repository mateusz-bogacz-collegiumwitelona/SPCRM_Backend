using Domain.Constants;
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

        private async Task<(Company Company, Currency Currency, ApplicationUser Owner)> SeedBasicInvoiceDependenciesAsync(string suffix)
        {
            var owner = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"U_{suffix}",
                NormalizedUserName = $"U_{suffix}".ToUpper(),
                Email = $"e_{suffix}@t.pl",
                NormalizedEmail = $"E_{suffix}@T.PL",
                FirstName = $"Imie_{suffix}",
                LastName = $"Nazwisko_{suffix}"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Stalex_{suffix}",
                NIP = "1234567890",
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

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.Currencies.Add(currency);
            await _contextMock.SaveChangesAsync();

            return (company, currency, owner);
        }

        // ─── GetCompanyDebtSummaryAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetCompanyDebtSummaryAsync_GroupsByCurrencyAndCalculatesCorrectAmount()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"U_{uniqueSuffix}",
                NormalizedUserName = $"U_{uniqueSuffix}",
                Email = $"e_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"E_{uniqueSuffix}@T.PL",
                FirstName = $"F_{uniqueSuffix}",
                LastName = $"L_{uniqueSuffix}",
            };
            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Company_{uniqueSuffix}",
                NIP = "111",
                OwnerId = ownerId,
                Owner = owner
            };

            var currencyPln = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "PLN",
                Code = "PLN",
                DecimalPlaces = 2
            };
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
                Company = company,
                CurrencyId = currencyPln.Id,
                Currency = currencyPln,
                TotalAmount = 10000000,
                PaidAmount = 5000000,
                DueDate = DateTime.UtcNow.AddDays(-5)
            };

            var invoice2 = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = "F/2",
                CompanyId = company.Id,
                Company = company,
                CurrencyId = currencyPln.Id,
                Currency = currencyPln,
                TotalAmount = 2000000,
                PaidAmount = 2000000,
                DueDate = DateTime.UtcNow.AddDays(-5)
            };

            var invoice3 = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = "F/3",
                CompanyId = company.Id,
                Company = company,
                CurrencyId = currencyPln.Id,
                Currency = currencyPln,
                TotalAmount = 3000000,
                PaidAmount = 0,
                DueDate = DateTime.UtcNow.AddDays(-5)
            };

            var invoice4 = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = "F/4",
                CompanyId = company.Id,
                Company = company,
                CurrencyId = currencyEur.Id,
                Currency = currencyEur,
                TotalAmount = 2000000,
                PaidAmount = 995000,
                DueDate = DateTime.UtcNow.AddDays(-5)
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.Currencies.AddRange(currencyPln, currencyEur);
            _contextMock.Invoices.AddRange(invoice1, invoice2, invoice3, invoice4);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _invoiceServicesMock.GetCompanyDebtSummaryAsync(company.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!).Count().IsEqualTo(2);

            var plnSummary = result.Data!.First(s => s.CurrencyCode == "PLN");
            await Assert.That(plnSummary.TotalAmount).IsEqualTo(8000000m);
            await Assert.That(plnSummary.DecimalPlace).IsEqualTo(2);

            var eurSummary = result.Data!.First(s => s.CurrencyCode == "EUR");
            await Assert.That(eurSummary.TotalAmount).IsEqualTo(1005000m);
        }

        [Test]
        public async Task GetCompanyDebtSummaryAsync_WhenNoDebts_ReturnsEmptyList()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var owner = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"U_{uniqueSuffix}",
                NormalizedUserName = $"U_{uniqueSuffix}",
                Email = $"e_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"E_{uniqueSuffix}@T.PL",
                FirstName = $"F_{uniqueSuffix}",
                LastName = $"L_{uniqueSuffix}",
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Company_{uniqueSuffix}",
                NIP = "111",
                OwnerId = owner.Id,
                Owner = owner
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "PLN",
                Code = "PLN",
                DecimalPlaces = 2
            };

            var invoice1 = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = "F/1",
                CompanyId = company.Id,
                Company = company,
                CurrencyId = currency.Id,
                Currency = currency,
                TotalAmount = 50000,
                PaidAmount = 50000,
                DueDate = DateTime.UtcNow.AddDays(-5)
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.Currencies.Add(currency);
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
            var uniqueSuffix = Guid.NewGuid().ToString("N");
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

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"CorruptedCompany_{uniqueSuffix}",
                NIP = "1234567890",
                OwnerId = ownerId,
                Owner = owner
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Test Currency",
                Code = currencyCode,
                DecimalPlaces = currencyDecimalPlaces
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.Currencies.Add(currency);

            var corruptedInvoice = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = $"INV/ERR/{uniqueSuffix}",
                CompanyId = company.Id,
                Company = company,
                CurrencyId = currency.Id,
                Currency = currency,
                TotalAmount = totalAmount,
                PaidAmount = paidAmount,
                DueDate = DateTime.UtcNow.AddDays(5)
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
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var owner = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"U_{uniqueSuffix}",
                NormalizedUserName = $"U_{uniqueSuffix}",
                Email = $"e_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"E_{uniqueSuffix}@T.PL",
                FirstName = $"F_{uniqueSuffix}",
                LastName = $"L_{uniqueSuffix}",
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Company_{uniqueSuffix}",
                NIP = "111",
                OwnerId = owner.Id,
                Owner = owner
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "PLN",
                Code = "PLN",
                DecimalPlaces = 2
            };

            var now = DateTime.UtcNow;

            var overdueInvoice = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = "INV/OVER",
                CompanyId = company.Id,
                Company = company,
                CurrencyId = currency.Id,
                Currency = currency,
                TotalAmount = 2000000,
                PaidAmount = 0,
                DueDate = now.AddDays(-10)
            };

            var futureInvoice = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = "INV/FUTURE",
                CompanyId = company.Id,
                Company = company,
                CurrencyId = currency.Id,
                Currency = currency,
                TotalAmount = 3000000,
                PaidAmount = 1000000,
                DueDate = now.AddDays(5)
            };

            var paidInvoice = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = "INV/PAID",
                CompanyId = company.Id,
                Company = company,
                CurrencyId = currency.Id,
                Currency = currency,
                TotalAmount = 5000000,
                PaidAmount = 5000000,
                DueDate = now.AddDays(-20)
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.Currencies.Add(currency);
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
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"User_{uniqueSuffix}",
                NormalizedUserName = $"USER_{uniqueSuffix}",
                Email = $"user_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"USER_{uniqueSuffix}@TEST.PL",
                FirstName = "Adam",
                LastName = "Nowak"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"CorruptedCompany_{uniqueSuffix}",
                NIP = "9876543210",
                OwnerId = ownerId,
                Owner = owner
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "EUR",
                Code = "EUR",
                DecimalPlaces = 2
            };

            var invalidInvoice = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = $"INV/BAD/{uniqueSuffix}",
                CompanyId = company.Id,
                Company = company,
                CurrencyId = currency.Id,
                Currency = currency,
                TotalAmount = 20000,
                PaidAmount = -500,
                DueDate = DateTime.UtcNow.AddDays(2)
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.Currencies.Add(currency);
            _contextMock.Invoices.Add(invalidInvoice);
            await _contextMock.SaveChangesAsync();

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
            // Arrange
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var (company, currency, _) = await SeedBasicInvoiceDependenciesAsync(suffix);

            var issueDate = DateTime.UtcNow.AddDays(-10);
            var dueDate = DateTime.UtcNow.AddDays(4);

            var invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = $"FV/TEST/{suffix}",
                CompanyId = company.Id,
                Company = company,
                CurrencyId = currency.Id,
                Currency = currency,
                TotalAmount = 5000000,
                PaidAmount = 2000000,
                IssueDate = issueDate,
                DueDate = dueDate
            };

            _contextMock.Invoices.Add(invoice);
            await _contextMock.SaveChangesAsync();

            var command = new InvoiceListCommand
            {
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
            var (company, currency, _) = await SeedBasicInvoiceDependenciesAsync(suffix);

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
                    DueDate = DateTime.UtcNow.AddDays(10)
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
            await Assert.That(result.Data.TotalCount).IsEqualTo(5);
            await Assert.That(result.Data.PageNumber).IsEqualTo(2);
        }

        [Test]
        public async Task GetInvoiceListAsync_SortsByTotalAmountAscendingAndDescending()
        {
            // Arrange
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var (company, currency, _) = await SeedBasicInvoiceDependenciesAsync(suffix);

            var invLow = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = $"FV/LOW/{suffix}",
                CompanyId = company.Id,
                CurrencyId = currency.Id,
                TotalAmount = 1000000,
                IssueDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(7)
            };

            var invHigh = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = $"FV/HIGH/{suffix}",
                CompanyId = company.Id,
                CurrencyId = currency.Id,
                TotalAmount = 9000000,
                IssueDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(7)
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
            var (company, currency, _) = await SeedBasicInvoiceDependenciesAsync(suffix);

            var now = DateTime.UtcNow;

            var matchingInvoice = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = $"FV/MATCH/{suffix}",
                CompanyId = company.Id,
                CurrencyId = currency.Id,
                TotalAmount = 5000000,
                IssueDate = now.AddDays(-5),
                DueDate = now.AddDays(10)
            };

            var nonMatchingAmount = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = $"FV/OUT_AMOUNT/{suffix}",
                CompanyId = company.Id,
                CurrencyId = currency.Id,
                TotalAmount = 1000000,
                IssueDate = now.AddDays(-5),
                DueDate = now.AddDays(10)
            };

            var nonMatchingDate = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = $"FV/OUT_DATE/{suffix}",
                CompanyId = company.Id,
                CurrencyId = currency.Id,
                TotalAmount = 5000000,
                IssueDate = now.AddDays(-30),
                DueDate = now.AddDays(10)
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
            var (company, currency, _) = await SeedBasicInvoiceDependenciesAsync(suffix);

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
                DueDate = now.AddDays(-5) 
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
                DueDate = now.AddDays(10) 
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
                DueDate = now.AddDays(-5) 
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
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var owner = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"U_{suffix}",
                Email = $"u_{suffix}@test.pl",
                FirstName = "Piotr",
                LastName = "Kowalski"
            };

            var accentedCompany = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Żelazo i Stal {suffix}",
                NIP = "9998887766",
                OwnerId = owner.Id,
                Owner = owner
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "PLN",
                Code = "PLN",
                DecimalPlaces = 2
            };

            var invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = $"FV/SEARCH/{suffix}",
                CompanyId = accentedCompany.Id,
                Company = accentedCompany,
                CurrencyId = currency.Id,
                Currency = currency,
                TotalAmount = 1000000,
                IssueDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(7)
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(accentedCompany);
            _contextMock.Currencies.Add(currency);
            _contextMock.Invoices.Add(invoice);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _invoiceServicesMock.GetInvoiceListAsync(new InvoiceListCommand
            {
                SearchTerm = "zelazo",
                PageNumber = 1,
                PageSize = 10
            });

            // Assert
            await Assert.That(result.Data!.Items).Count().IsEqualTo(1);
            await Assert.That(result.Data.Items.First().InvoiceNumber).IsEqualTo(invoice.InvoiceNumber);
        }
    }
}
