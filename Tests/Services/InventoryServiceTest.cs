using Domain.Constants;
using Domain.Enum;
using Domain.Models;
using Infrastructure;
using Infrastructure.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Services.Services;
using Testcontainers.PostgreSql;

namespace Tests.Services
{
    public class InventoryServiceTest
    {
        protected AppDbContext _contextMock = null!;

        private static PostgreSqlContainer _dbContainer = null!;
        private static string _connectionString = null!;

        protected InventoryService _inventoryServiceMock = null!;
        protected ILogger<InventoryService> _loggerMock = null!;

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

            _loggerMock = new LoggerFactory().CreateLogger<InventoryService>();

            _inventoryServiceMock = new InventoryService(_contextMock, _loggerMock);

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

        // ─── ValidateStockAvailabilityAsync ───────────────────────────────────

        [Test]
        public async Task ValidateStockAvailabilityAsync_WhenProductDoesNotExist_Returns404NotFound()
        {
            // Arrange
            var nonExistentProductId = Guid.NewGuid();

            // Act
            var result = await _inventoryServiceMock.ValidateStockAvailabilityAsync(nonExistentProductId, 5);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.ProductNotFound);
        }

        [Test]
        public async Task ValidateStockAvailabilityAsync_WhenStockIsSufficient_ReturnsSuccess()
        {
            // Arrange
            var unique = Guid.NewGuid().ToString("N")[..8];
            var userId = Guid.NewGuid();

            var user = new ApplicationUser
            {
                Id = userId,
                UserName = $"User_{unique}",
                NormalizedUserName = $"USER_{unique}",
                Email = $"user_{unique}@test.com",
                NormalizedEmail = $"USER_{unique}@TEST.COM",
                FirstName = "Jan",
                LastName = "Kowalski"
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "PLN",
                Code = "PLN",
                DecimalPlaces = 2
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Firma_{unique}",
                NIP = "1234567890",
                OwnerId = userId,
                Owner = user
            };

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Anna",
                LastName = "Nowak",
                IsPrimary = true,
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = user
            };

            var steelGrade = new SteelGrade { Id = Guid.NewGuid(), Name = "S235", Density = 7850 };
            var unit = new UnitOfMeasure { Id = Guid.NewGuid(), Name = "Sztuka", Symbol = "szt." };

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Blacha S235",
                SteelGradeId = steelGrade.Id,
                SteelGrade = steelGrade,
                UnitId = unit.Id,
                Unit = unit,
                CurrencyId = currency.Id,
                Currency = currency,
                StockQuantity = 20,
                Category = ProductCategoryEnum.Sheet
            };

            _contextMock.Users.Add(user);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.Add(contact);
            _contextMock.SteelGrades.Add(steelGrade);
            _contextMock.UnitsOfMeasure.Add(unit);
            _contextMock.Products.Add(product);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _inventoryServiceMock.ValidateStockAvailabilityAsync(product.Id, 15);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
        }

        [Test]
        public async Task ValidateStockAvailabilityAsync_WhenReservedStockExceedsAvailable_ReturnsBadRequest()
        {
            // Arrange
            var unique = Guid.NewGuid().ToString("N")[..8];
            var userId = Guid.NewGuid();

            var user = new ApplicationUser
            {
                Id = userId,
                UserName = $"User_{unique}",
                NormalizedUserName = $"USER_{unique}",
                Email = $"user_{unique}@test.com",
                NormalizedEmail = $"USER_{unique}@TEST.COM",
                FirstName = "Jan",
                LastName = "Kowalski"
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "PLN",
                Code = "PLN",
                DecimalPlaces = 2
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Firma_{unique}",
                NIP = "1234567890",
                OwnerId = userId,
                Owner = user
            };

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Anna",
                LastName = "Nowak",
                IsPrimary = true,
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = user
            };

            var steelGrade = new SteelGrade { Id = Guid.NewGuid(), Name = "S235", Density = 7850 };
            var unit = new UnitOfMeasure { Id = Guid.NewGuid(), Name = "Sztuka", Symbol = "szt." };

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Rura Stalowa",
                SteelGradeId = steelGrade.Id,
                SteelGrade = steelGrade,
                UnitId = unit.Id,
                Unit = unit,
                CurrencyId = currency.Id,
                Currency = currency,
                StockQuantity = 10,
                Category = ProductCategoryEnum.Pipe
            };

            var activeDeal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Deal Rezerwacyjny",
                Value = 80000,
                Status = DealsStatusEnum.ToDo,
                CloseDate = DateTime.UtcNow.AddDays(7),
                CompanyId = company.Id,
                OwnerId = userId,
                CurrencyId = currency.Id,
                ContactId = contact.Id,
                DealProducts = new List<DealProduct>
                {
                    new() { ProductId = product.Id, Quantity = 8, UnitPrice = 10000 }
                }
            };

            _contextMock.Users.Add(user);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.Add(contact);
            _contextMock.SteelGrades.Add(steelGrade);
            _contextMock.UnitsOfMeasure.Add(unit);
            _contextMock.Products.Add(product);
            _contextMock.Deals.Add(activeDeal);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _inventoryServiceMock.ValidateStockAvailabilityAsync(product.Id, 5);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);
        }

        // ─── DeductStockForDealAsync ──────────────────────────────────────────

        [Test]
        public async Task DeductStockForDealAsync_WhenProductNotFound_Returns404NotFound()
        {
            // Arrange
            var nonExistentProductId = Guid.NewGuid();
            var dealProducts = new List<DealProduct>
            {
                new() { ProductId = nonExistentProductId, Quantity = 5, UnitPrice = 1000 }
            };

            // Act
            var result = await _inventoryServiceMock.DeductStockForDealAsync(dealProducts);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.ProductNotFound);
        }

        [Test]
        public async Task DeductStockForDealAsync_WhenStockIsInsufficient_ReturnsBadRequest()
        {
            // Arrange
            var steelGrade = new SteelGrade { Id = Guid.NewGuid(), Name = "S235", Density = 7850 };
            var unit = new UnitOfMeasure { Id = Guid.NewGuid(), Name = "Sztuka", Symbol = "szt." };
            var currency = new Currency { Id = Guid.NewGuid(), Name = "PLN", Code = "PLN", DecimalPlaces = 2 };

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Towar Deficytowy",
                SteelGradeId = steelGrade.Id,
                SteelGrade = steelGrade,
                UnitId = unit.Id,
                Unit = unit,
                CurrencyId = currency.Id,
                Currency = currency,
                StockQuantity = 3,
                Category = ProductCategoryEnum.Bar
            };

            _contextMock.SteelGrades.Add(steelGrade);
            _contextMock.UnitsOfMeasure.Add(unit);
            _contextMock.Currencies.Add(currency);
            _contextMock.Products.Add(product);
            await _contextMock.SaveChangesAsync();

            var dealProducts = new List<DealProduct>
            {
                new() { ProductId = product.Id, Quantity = 10, UnitPrice = 1000 }
            };

            // Act
            var result = await _inventoryServiceMock.DeductStockForDealAsync(dealProducts);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InsufficientStock);
        }

        [Test]
        public async Task DeductStockForDealAsync_WhenStockIsSufficient_ReducesStockAndReturnsSuccess()
        {
            // Arrange
            var steelGrade = new SteelGrade { Id = Guid.NewGuid(), Name = "S235", Density = 7850 };
            var unit = new UnitOfMeasure { Id = Guid.NewGuid(), Name = "Sztuka", Symbol = "szt." };
            var currency = new Currency { Id = Guid.NewGuid(), Name = "PLN", Code = "PLN", DecimalPlaces = 2 };

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Towar Dostępny",
                SteelGradeId = steelGrade.Id,
                SteelGrade = steelGrade,
                UnitId = unit.Id,
                Unit = unit,
                CurrencyId = currency.Id,
                Currency = currency,
                StockQuantity = 50,
                Category = ProductCategoryEnum.Bar
            };

            _contextMock.SteelGrades.Add(steelGrade);
            _contextMock.UnitsOfMeasure.Add(unit);
            _contextMock.Currencies.Add(currency);
            _contextMock.Products.Add(product);
            await _contextMock.SaveChangesAsync();

            var dealProducts = new List<DealProduct>
            {
                new() { ProductId = product.Id, Quantity = 15, UnitPrice = 1000 }
            };

            // Act
            var result = await _inventoryServiceMock.DeductStockForDealAsync(dealProducts);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            await _contextMock.SaveChangesAsync();

            var updatedProduct = await _contextMock.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == product.Id);
            await Assert.That(updatedProduct!.StockQuantity).IsEqualTo(35);
        }
    }
}
