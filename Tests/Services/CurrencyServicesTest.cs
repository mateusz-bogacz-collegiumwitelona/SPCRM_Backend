using Domain.Models;
using Infrastructure;
using Infrastructure.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Services.Command;
using Services.Services;
using Testcontainers.PostgreSql;

namespace Tests.Services
{
    public class CurrencyServicesTest
    {
        protected AppDbContext _contextMock = null!;
        protected CurrencyServices _currencyServicesMock = null!;
        protected ILogger<CurrencyServices> _loggerMock = null!;
        private static PostgreSqlContainer _dbContainer = null!;
        private static string _connectionString = null!;
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

            _loggerMock = new LoggerFactory().CreateLogger<CurrencyServices>();

            _currencyServicesMock = new CurrencyServices(_contextMock, _loggerMock);
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

        // ─── GetCurrencySimpleListAsync ──────────────────────────────────────────

        [Test]
        public async Task GetCurrencySimpleListAsync_WhenCurrenciesExist_ReturnsAllMappedCurrencies()
        {
            // Arrange
            var currencyPln = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Polski Złoty",
                Code = "PLN",
                DecimalPlaces = 2
            };

            var currencyEur = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Euro",
                Code = "EUR",
                DecimalPlaces = 2
            };

            var currencyUsd = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "US Dollar",
                Code = "USD",
                DecimalPlaces = 2
            };

            _contextMock.Currencies.AddRange(currencyPln, currencyEur, currencyUsd);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _currencyServicesMock.GetCurrencySimpleListAsync();

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("Currency list retrieved successfully.");
            await Assert.That(result.Data).IsNotNull();

            var items = result.Data!;
            await Assert.That(items).Count().IsEqualTo(3);

            var mappedPln = items.FirstOrDefault(c => c.CurrencyId == currencyPln.Id);
            await Assert.That(mappedPln).IsNotNull();
            await Assert.That(mappedPln!.Name).IsEqualTo("Polski Złoty");
            await Assert.That(mappedPln.Code).IsEqualTo("PLN");
            await Assert.That(mappedPln.DecimalPlace).IsEqualTo(2);
        }

        [Test]
        public async Task GetCurrencySimpleListAsync_WhenNoCurrenciesInDatabase_ReturnsEmptyListWithSuccessStatus()
        {
            // Act
            var result = await _currencyServicesMock.GetCurrencySimpleListAsync();

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!).IsEmpty();
        }

        // ─── GetCurrenyListAsync ────────────────────────────────────────────────

        [Test]
        public async Task GetCurrenyListAsync_WhenStandardRequest_ReturnsPagedResult()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var currency1 = new Currency
            {
                Id = Guid.NewGuid(),
                Name = $"Złoty_{uniqueSuffix}",
                Code = $"PLN_{uniqueSuffix}",
                DecimalPlaces = 2
            };

            var currency2 = new Currency
            {
                Id = Guid.NewGuid(),
                Name = $"Euro_{uniqueSuffix}",
                Code = $"EUR_{uniqueSuffix}",
                DecimalPlaces = 2
            };

            _contextMock.Currencies.AddRange(currency1, currency2);
            await _contextMock.SaveChangesAsync();

            var command = new BasicListCommand
            {
                PageNumber = 1,
                PageSize = 10,
                SearchTerm = uniqueSuffix
            };

            // Act
            var result = await _currencyServicesMock.GetCurrenyListAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.Items.Count()).IsEqualTo(2);
            await Assert.That(result.Data.TotalCount).IsEqualTo(2);
        }

        [Test]
        public async Task GetCurrenyListAsync_WhenSearchApplied_FiltersByNameOrCode()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var currency1 = new Currency
            {
                Id = Guid.NewGuid(),
                Name = $"Funt_{uniqueSuffix}",
                Code = $"GBP_{uniqueSuffix}",
                DecimalPlaces = 2
            };

            var currency2 = new Currency
            {
                Id = Guid.NewGuid(),
                Name = $"Frank_{uniqueSuffix}",
                Code = $"CHF_{uniqueSuffix}",
                DecimalPlaces = 2
            };

            _contextMock.Currencies.AddRange(currency1, currency2);
            await _contextMock.SaveChangesAsync();

            var command = new BasicListCommand
            {
                PageNumber = 1,
                PageSize = 10,
                SearchTerm = $"GBP_{uniqueSuffix}"
            };

            // Act
            var result = await _currencyServicesMock.GetCurrenyListAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data!.Items.Count()).IsEqualTo(1);
            await Assert.That(result.Data.Items.First().Code).IsEqualTo($"GBP_{uniqueSuffix}");
        }

        [Test]
        public async Task GetCurrenyListAsync_WhenSortingApplied_OrdersCorrectly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var currencyA = new Currency
            {
                Id = Guid.NewGuid(),
                Name = $"A_Waluta_{uniqueSuffix}",
                Code = $"AAA_{uniqueSuffix}",
                DecimalPlaces = 2
            };

            var currencyZ = new Currency
            {
                Id = Guid.NewGuid(),
                Name = $"Z_Waluta_{uniqueSuffix}",
                Code = $"ZZZ_{uniqueSuffix}",
                DecimalPlaces = 2
            };

            _contextMock.Currencies.AddRange(currencyA, currencyZ);
            await _contextMock.SaveChangesAsync();

            var command = new BasicListCommand
            {
                PageNumber = 1,
                PageSize = 10,
                SearchTerm = uniqueSuffix,
                SortBy = "name",
                SortDescending = true
            };

            // Act
            var result = await _currencyServicesMock.GetCurrenyListAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            var items = result.Data!.Items.ToList();
            await Assert.That(items.First().Name).IsEqualTo($"Z_Waluta_{uniqueSuffix}");
            await Assert.That(items.Last().Name).IsEqualTo($"A_Waluta_{uniqueSuffix}");
        }

        [Test]
        public async Task GetCurrenyListAsync_WhenEmpty_ReturnsEmptyPagedResult()
        {
            // Arrange
            var command = new BasicListCommand
            {
                PageNumber = 1,
                PageSize = 10,
                SearchTerm = "non_existent_currency_search_term"
            };

            // Act
            var result = await _currencyServicesMock.GetCurrenyListAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data!.Items).IsEmpty();
            await Assert.That(result.Data.TotalCount).IsEqualTo(0);
        }
    }
}
