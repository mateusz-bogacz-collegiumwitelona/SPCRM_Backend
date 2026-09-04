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
using Services.Services;
using Testcontainers.PostgreSql;

namespace Tests.Services
{
    public class DebtServiceTest
    {
        protected AppDbContext _contextMock = null!;

        private static PostgreSqlContainer _dbContainer = null!;
        private static string _connectionString = null!;

        protected DebtService _debtServicesMock = null!;
        protected ILogger<DebtService> _loggerMock = null!;

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

            _loggerMock = new LoggerFactory().CreateLogger<DebtService>();
            _debtServicesMock = new DebtService(_contextMock, _loggerMock);
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
            var result = await _debtServicesMock.GetCompanyDebtSummaryAsync(company.Id);

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
            var result = await _debtServicesMock.GetCompanyDebtSummaryAsync(company.Id);

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
            var result = await _debtServicesMock.GetCompanyDebtSummaryAsync(nonExistentCompanyId);

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
            await Assert.That(async () => await _debtServicesMock.GetCompanyDebtSummaryAsync(company.Id))
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
            var result = await _debtServicesMock.GetCompanyDebtsAsync(command);

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
            var result = await _debtServicesMock.GetCompanyDebtsAsync(command);

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
            await Assert.That(async () => await _debtServicesMock.GetCompanyDebtsAsync(command))
                .Throws<DataCorruptionException>();
        }
    }
}
