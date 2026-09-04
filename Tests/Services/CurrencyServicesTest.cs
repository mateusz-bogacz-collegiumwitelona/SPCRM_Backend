using Domain.Constants;
using Domain.Exceptions.Exception;
using Domain.Models;
using Infrastructure;
using Infrastructure.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Services.Command.Currency;
using Services.Command.List;
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

        [Test]
        public async Task GetCurrencySimpleListAsync_WhenCurrenciesExist_ReturnsCurrenciesSortedByCode()
        {
            // Arrange
            var currencyUsd = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "US Dollar",
                Code = "USD",
                DecimalPlaces = 2
            };

            var currencyEur = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Euro",
                Code = "EUR",
                DecimalPlaces = 2
            };

            var currencyPln = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Polski Złoty",
                Code = "PLN",
                DecimalPlaces = 2
            };

            _contextMock.Currencies.AddRange(currencyUsd, currencyEur, currencyPln);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _currencyServicesMock.GetCurrencySimpleListAsync();

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var codes = result.Data!.Select(c => c.Code).ToList();
            await Assert.That(codes).IsEquivalentTo(new[] { "EUR", "PLN", "USD" });
            await Assert.That(codes[0]).IsEqualTo("EUR");
            await Assert.That(codes[1]).IsEqualTo("PLN");
            await Assert.That(codes[2]).IsEqualTo("USD");
        }

        [Test]
        [Arguments("", "Polski Złoty", 2)]
        [Arguments("PLN", "", 2)]
        [Arguments("PLN", "Polski Złoty", -1)]
        public async Task GetCurrencySimpleListAsync_WhenDatabaseContainsCorruptedCurrency_ThrowsDataCorruptionException(
            string code,
            string name,
            int decimalPlaces)
        {
            // Arrange
            var corruptedCurrency = new Currency
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = name,
                DecimalPlaces = decimalPlaces
            };

            _contextMock.Currencies.Add(corruptedCurrency);
            await _contextMock.SaveChangesAsync();

            // Act & Assert
            await Assert.That(async () => await _currencyServicesMock.GetCurrencySimpleListAsync())
                .Throws<DataCorruptionException>();
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

        // ─── AddCurrencyAsync ───────────────────────────────────────────────────

        [Test]
        public async Task AddCurrencyAsync_WhenValidDataProvided_CreatesCurrencyAndReturns201Created()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N").Substring(0, 4).ToUpper();
            var command = new AddCurrencyCommand
            {
                Name = $"Dolar Kanadyjski_{uniqueSuffix}",
                Code = $"CAD",
                DecimalPlaces = 2
            };

            // Act
            var result = await _currencyServicesMock.AddCurrencyAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status201Created);
            await Assert.That(result.Message).IsEqualTo("Currency added successfully.");

            var createdCurrency = await _contextMock.Currencies
                .FirstOrDefaultAsync(c => c.Code == command.Code);

            await Assert.That(createdCurrency).IsNotNull();
            await Assert.That(createdCurrency!.Name).IsEqualTo(command.Name);
            await Assert.That(createdCurrency.DecimalPlaces).IsEqualTo(2);
        }

        [Test]
        public async Task AddCurrencyAsync_WhenCurrencyCodeAlreadyExists_ReturnsBadRequestAndConflictErrorCode()
        {
            // Arrange
            var existingCurrency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Funt Brytyjski",
                Code = "GBP",
                DecimalPlaces = 2
            };
            _contextMock.Currencies.Add(existingCurrency);
            await _contextMock.SaveChangesAsync();

            var command = new AddCurrencyCommand
            {
                Name = "Inny Funt",
                Code = "GBP",
                DecimalPlaces = 2
            };

            // Act
            var result = await _currencyServicesMock.AddCurrencyAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.CurrencyAlreadyExists);
        }

        [Test]
        public async Task AddCurrencyAsync_WhenCurrencyNameAlreadyExistsCaseInsensitive_ReturnsBadRequest()
        {
            // Arrange
            var existingCurrency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Frank Szwajcarski",
                Code = "CHF",
                DecimalPlaces = 2
            };
            _contextMock.Currencies.Add(existingCurrency);
            await _contextMock.SaveChangesAsync();

            var command = new AddCurrencyCommand
            {
                Name = "frank szwajcarski",
                Code = "SWF",
                DecimalPlaces = 2
            };

            // Act
            var result = await _currencyServicesMock.AddCurrencyAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.CurrencyAlreadyExists);
        }

        // ─── EditCurrencyAsync ──────────────────────────────────────────────────

        [Test]
        public async Task EditCurrencyAsync_WhenCurrencyExistsAndDataIsValid_UpdatesFieldsAndReturns200OK()
        {
            // Arrange
            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Stara Nazwa",
                Code = "OLD",
                DecimalPlaces = 2
            };

            _contextMock.Currencies.Add(currency);
            await _contextMock.SaveChangesAsync();

            var command = new EditCurrencyCommand
            {
                CurrencyId = currency.Id,
                Name = "Nowa Nazwa",
                Code = "NEW",
                DecimalPlaces = 4
            };

            // Act
            var result = await _currencyServicesMock.EditCurrencyAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("Currency updated successfully.");

            var updatedCurrency = await _contextMock.Currencies.FindAsync(currency.Id);
            await Assert.That(updatedCurrency).IsNotNull();
            await Assert.That(updatedCurrency!.Name).IsEqualTo("Nowa Nazwa");
            await Assert.That(updatedCurrency.Code).IsEqualTo("NEW");
            await Assert.That(updatedCurrency.DecimalPlaces).IsEqualTo(4);
        }

        [Test]
        public async Task EditCurrencyAsync_WhenPartialDataProvided_UpdatesOnlySpecifiedFields()
        {
            // Arrange
            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Oryginalna Nazwa",
                Code = "ORG",
                DecimalPlaces = 2
            };

            _contextMock.Currencies.Add(currency);
            await _contextMock.SaveChangesAsync();

            var command = new EditCurrencyCommand
            {
                CurrencyId = currency.Id,
                Name = "Zmieniona Tylko Nazwa",
                Code = null,
                DecimalPlaces = null
            };

            // Act
            var result = await _currencyServicesMock.EditCurrencyAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();

            var updatedCurrency = await _contextMock.Currencies.FindAsync(currency.Id);
            await Assert.That(updatedCurrency).IsNotNull();
            await Assert.That(updatedCurrency!.Name).IsEqualTo("Zmieniona Tylko Nazwa");
            await Assert.That(updatedCurrency.Code).IsEqualTo("ORG");
            await Assert.That(updatedCurrency.DecimalPlaces).IsEqualTo(2);
        }

        [Test]
        public async Task EditCurrencyAsync_WhenCurrencyNotFound_Returns404NotFound()
        {
            // Arrange
            var command = new EditCurrencyCommand
            {
                CurrencyId = Guid.NewGuid(),
                Name = "Nieistniejąca",
                Code = "NON",
                DecimalPlaces = 2
            };

            // Act
            var result = await _currencyServicesMock.EditCurrencyAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.CurrencyNotFound);
        }

        [Test]
        public async Task EditCurrencyAsync_WhenNameCollidesWithAnotherCurrency_Returns409Conflict()
        {
            // Arrange
            var currencyToEdit = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Waluta A",
                Code = "WLA",
                DecimalPlaces = 2
            };

            var anotherCurrency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Zajęta Nazwa",
                Code = "ZNT",
                DecimalPlaces = 2
            };

            _contextMock.Currencies.AddRange(currencyToEdit, anotherCurrency);
            await _contextMock.SaveChangesAsync();

            var command = new EditCurrencyCommand
            {
                CurrencyId = currencyToEdit.Id,
                Name = "zajęta nazwa"
            };

            // Act
            var result = await _currencyServicesMock.EditCurrencyAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status409Conflict);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.CurrencyNameAlreadyExists);
        }

        [Test]
        public async Task EditCurrencyAsync_WhenCodeCollidesWithAnotherCurrency_Returns409Conflict()
        {
            // Arrange
            var currencyToEdit = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Waluta B",
                Code = "WLB",
                DecimalPlaces = 2
            };

            var anotherCurrency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Waluta C",
                Code = "WLC",
                DecimalPlaces = 2
            };

            _contextMock.Currencies.AddRange(currencyToEdit, anotherCurrency);
            await _contextMock.SaveChangesAsync();

            var command = new EditCurrencyCommand
            {
                CurrencyId = currencyToEdit.Id,
                Code = "WLC"
            };

            // Act
            var result = await _currencyServicesMock.EditCurrencyAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status409Conflict);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.CurrencyCodeAlreadyExists);
        }

        [Test]
        public async Task EditCurrencyAsync_WhenKeepingSameNameAndCode_SucceedsWithoutConflict()
        {
            // Arrange
            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Testowa Waluta",
                Code = "TST",
                DecimalPlaces = 2
            };

            _contextMock.Currencies.Add(currency);
            await _contextMock.SaveChangesAsync();

            var command = new EditCurrencyCommand
            {
                CurrencyId = currency.Id,
                Name = "Testowa Waluta",
                Code = "TST",
                DecimalPlaces = 3
            };

            // Act
            var result = await _currencyServicesMock.EditCurrencyAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var updatedCurrency = await _contextMock.Currencies.FindAsync(currency.Id);
            await Assert.That(updatedCurrency!.DecimalPlaces).IsEqualTo(3);
        }

        [Test]
        [Arguments("", "Polski Złoty", 2)]
        [Arguments("PLN", "", 2)]
        [Arguments("PLN", "Polski Złoty", -1)]
        public async Task EditCurrencyAsync_WhenCurrencyInDatabaseHasCorruptedState_ThrowsDataCorruptionException(
            string code,
            string name,
            int decimalPlaces)
        {
            // Arrange
            var corruptedCurrency = new Currency
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = name,
                DecimalPlaces = decimalPlaces
            };

            _contextMock.Currencies.Add(corruptedCurrency);
            await _contextMock.SaveChangesAsync();

            var command = new EditCurrencyCommand
            {
                CurrencyId = corruptedCurrency.Id,
                Name = "Nowa Prawidłowa Nazwa",
                Code = "NEW",
                DecimalPlaces = 2
            };

            // Act & Assert
            await Assert.That(async () => await _currencyServicesMock.EditCurrencyAsync(command))
                .Throws<DataCorruptionException>();
        }
    }
}
