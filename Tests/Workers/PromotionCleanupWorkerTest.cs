using Domain.Enum;
using Domain.Models;
using Infrastructure;
using Infrastructure.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Testcontainers.PostgreSql;
using Worker.Workers;

namespace Tests.Workers
{
    public class PromotionCleanupWorkerTest
    {
        private AppDbContext _contextMock = null!;
        private static PostgreSqlContainer _dbContainer = null!;
        private static string _connectionString = null!;
        private string _currentSchema = null!;

        private ILogger<PromotionCleanupWorker> _loggerMock = null!;
        private PromotionCleanupWorker _worker = null!;

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

            _loggerMock = new LoggerFactory().CreateLogger<PromotionCleanupWorker>();
            _worker = new PromotionCleanupWorker(_contextMock, _loggerMock);
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

        private async Task<Product> CreateDummyProductAsync()
        {
            var unit = new UnitOfMeasure { Id = Guid.NewGuid(), Name = "Sztuka", Symbol = "szt", BaseMultiplier = 1 };
            _contextMock.UnitsOfMeasure.Add(unit);

            var steelGrade = new SteelGrade
            {
                Id = Guid.NewGuid(),
                Name = "S235"
            };
            _contextMock.SteelGrades.Add(steelGrade);

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Polski Złoty",
                Code = "PLN",
                DecimalPlaces = 2
            };
            _contextMock.Currencies.Add(currency);

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Test Product " + Guid.NewGuid().ToString("N")[..6],
                SteelGradeId = steelGrade.Id,
                SteelGrade = steelGrade,
                Thickness = 10,
                Width = 100,
                Length = 1000,
                Weight = 50,
                PricePerUnit = 100000,
                StockQuantity = 10,
                Category = ProductCategoryEnum.Pipe,
                UnitId = unit.Id,
                Unit = unit,
                CurrencyId = currency.Id,
                Currency = currency
            };

            _contextMock.Products.Add(product);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            return product;
        }

        private async Task<Promotion> SeedPromotionAsync(
            Guid productId,
            bool isActive,
            DateTime? endDate,
            string? name = null)
        {
            var promotion = new Promotion
            {
                Id = Guid.NewGuid(),
                Name = name ?? "Promo_" + Guid.NewGuid().ToString("N")[..6],
                ProductId = productId,
                IsActive = isActive,
                DiscountPercentage = 10,
                StartDate = DateTime.UtcNow.AddMonths(-1),
                EndDate = endDate
            };

            _contextMock.Promotions.Add(promotion);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            return promotion;
        }

        // ─── CleanupExpiredPromotionsAsync ────────────────────────────────────────────────────────────

        [Test]
        public async Task CleanupExpiredPromotionsAsync_WhenPromotionIsActiveAndEndDatePassed_DeactivatesPromotion()
        {
            // Arrange
            var product = await CreateDummyProductAsync();
            var pastEndDate = DateTime.UtcNow.AddDays(-2);

            var promo = await SeedPromotionAsync(product.Id, isActive: true, endDate: pastEndDate);

            // Act
            await _worker.CleanupExpiredPromotionsAsync();

            // Assert
            var updatedPromo = await _contextMock.Promotions
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == promo.Id);

            await Assert.That(updatedPromo).IsNotNull();
            await Assert.That(updatedPromo!.IsActive).IsFalse();
        }

        [Test]
        public async Task CleanupExpiredPromotionsAsync_WhenPromotionIsActiveAndEndDateInFuture_LeavesPromotionActive()
        {
            // Arrange
            var product = await CreateDummyProductAsync();
            var futureEndDate = DateTime.UtcNow.AddDays(7);

            var promo = await SeedPromotionAsync(product.Id, isActive: true, endDate: futureEndDate);

            // Act
            await _worker.CleanupExpiredPromotionsAsync();

            // Assert
            var updatedPromo = await _contextMock.Promotions
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == promo.Id);

            await Assert.That(updatedPromo).IsNotNull();
            await Assert.That(updatedPromo!.IsActive).IsTrue();
        }

        [Test]
        public async Task CleanupExpiredPromotionsAsync_WhenPromotionIsActiveWithoutEndDate_LeavesPromotionActive()
        {
            // Arrange
            var product = await CreateDummyProductAsync();

            var promo = await SeedPromotionAsync(product.Id, isActive: true, endDate: null);

            // Act
            await _worker.CleanupExpiredPromotionsAsync();

            // Assert
            var updatedPromo = await _contextMock.Promotions
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == promo.Id);

            await Assert.That(updatedPromo).IsNotNull();
            await Assert.That(updatedPromo!.IsActive).IsTrue();
        }

        [Test]
        public async Task CleanupExpiredPromotionsAsync_WhenPromotionAlreadyInactive_DoesNotModifyIt()
        {
            // Arrange
            var product = await CreateDummyProductAsync();
            var pastEndDate = DateTime.UtcNow.AddDays(-5);

            var promo = await SeedPromotionAsync(product.Id, isActive: false, endDate: pastEndDate);

            // Act
            await _worker.CleanupExpiredPromotionsAsync();

            // Assert
            var updatedPromo = await _contextMock.Promotions
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == promo.Id);

            await Assert.That(updatedPromo).IsNotNull();
            await Assert.That(updatedPromo!.IsActive).IsFalse();
        }

        [Test]
        public async Task CleanupExpiredPromotionsAsync_WhenMultiplePromotionsExist_OnlyDeactivatesMatchingOnes()
        {
            // Arrange
            var product = await CreateDummyProductAsync();

            var toDeactivate = await SeedPromotionAsync(product.Id, isActive: true, endDate: DateTime.UtcNow.AddDays(-1), "DO_WYGASZENIA");
            var futureActive = await SeedPromotionAsync(product.Id, isActive: true, endDate: DateTime.UtcNow.AddDays(5), "TRWAJĄCA");
            var indefiniteActive = await SeedPromotionAsync(product.Id, isActive: true, endDate: null, "BEZTERMINOWA");
            var alreadyInactive = await SeedPromotionAsync(product.Id, isActive: false, endDate: DateTime.UtcNow.AddDays(-10), "JUŻ_NIEAKTYWNA");

            // Act
            await _worker.CleanupExpiredPromotionsAsync();

            // Assert
            var allPromos = await _contextMock.Promotions.AsNoTracking().ToListAsync();

            var resultToDeactivate = allPromos.First(p => p.Id == toDeactivate.Id);
            var resultFutureActive = allPromos.First(p => p.Id == futureActive.Id);
            var resultIndefiniteActive = allPromos.First(p => p.Id == indefiniteActive.Id);
            var resultAlreadyInactive = allPromos.First(p => p.Id == alreadyInactive.Id);

            await Assert.That(resultToDeactivate.IsActive).IsFalse();
            await Assert.That(resultFutureActive.IsActive).IsTrue();
            await Assert.That(resultIndefiniteActive.IsActive).IsTrue();
            await Assert.That(resultAlreadyInactive.IsActive).IsFalse();
        }

        [Test]
        public async Task CleanupExpiredPromotionsAsync_WhenNoPromotionsToDeactivate_CompletesCleanly()
        {
            // Arrange 
            var product = await CreateDummyProductAsync();
            await SeedPromotionAsync(product.Id, isActive: true, endDate: DateTime.UtcNow.AddDays(14));

            // Act & Assert 
            await _worker.CleanupExpiredPromotionsAsync();

            var deactivatedCount = await _contextMock.Promotions.CountAsync(p => !p.IsActive);
            await Assert.That(deactivatedCount).IsEqualTo(0);
        }

        [Test]
        public async Task CleanupExpiredPromotionsAsync_WhenPromotionIsSoftDeleted_DoesNotProcessIt()
        {
            // Arrange 
            var product = await CreateDummyProductAsync();

            var softDeletedPromo = new Promotion
            {
                Id = Guid.NewGuid(),
                Name = "Usunięta Promocja",
                ProductId = product.Id,
                IsActive = true,
                StartDate = DateTime.UtcNow.AddMonths(-1),
                EndDate = DateTime.UtcNow.AddDays(-3),
                IsDeleted = true
            };

            _contextMock.Promotions.Add(softDeletedPromo);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            // Act
            await _worker.CleanupExpiredPromotionsAsync();

            // Assert 
            var promoInDb = await _contextMock.Promotions
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == softDeletedPromo.Id);

            await Assert.That(promoInDb).IsNotNull();
            await Assert.That(promoInDb!.IsActive).IsTrue();
        }
    }
}
