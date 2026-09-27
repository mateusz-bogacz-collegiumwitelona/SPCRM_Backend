using Domain.Enum;
using Domain.Events;
using Domain.Exceptions.Exception;
using Domain.Models;
using Infrastructure;
using Infrastructure.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Services.Handlers;
using Services.Interfaces;
using Services.Services;
using Testcontainers.PostgreSql;

namespace Tests.Handler
{
    public class OfferAcceptedEventHandlerTest
    {
        private AppDbContext _contextMock = null!;
        private static PostgreSqlContainer _dbContainer = null!;
        private static string _connectionString = null!;
        private string _currentSchema = null!;

        private IInventoryService _inventoryMock = null!;
        private ILogger<OfferAcceptedEventHandler> _loggerMock = null!;
        private OfferAcceptedEventHandler _handler = null!;

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

            _loggerMock = new LoggerFactory().CreateLogger<OfferAcceptedEventHandler>();

            _inventoryMock = new InventoryService(
                _contextMock,
                new LoggerFactory().CreateLogger<InventoryService>());

            _handler = new OfferAcceptedEventHandler(
                _contextMock,
                _inventoryMock,
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


        private async Task<(Company Company, Contact Contact, Currency Currency, ApplicationUser Owner, SteelGrade SteelGrade, UnitOfMeasure Unit)> SeedBaseGraphAsync()
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "user_" + Guid.NewGuid().ToString("N"),
                Email = "user_" + Guid.NewGuid().ToString("N") + "@test.pl",
                FirstName = "Adam",
                LastName = "Kowalski"
            };
            _contextMock.Users.Add(user);

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Polski Złoty",
                Code = "PLN",
                DecimalPlaces = 2,
                IsDeleted = false
            };
            _contextMock.Currencies.Add(currency);

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = "Stal-Met Sp. z o.o.",
                NIP = "1234567890",
                OwnerId = user.Id
            };
            _contextMock.Companies.Add(company);

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Piotr",
                LastName = "Nowak",
                CompanyId = company.Id,
                OwnerId = user.Id,
                IsPrimary = true,
                Owner = user
            };
            _contextMock.Contacts.Add(contact);

            var steelGrade = new SteelGrade { Id = Guid.NewGuid(), Name = "1.4301", Density = 7900 };
            var unit = new UnitOfMeasure { Id = Guid.NewGuid(), Name = "Sztuka", Symbol = "szt.", BaseMultiplier = 1 };

            _contextMock.SteelGrades.Add(steelGrade);
            _contextMock.UnitsOfMeasure.Add(unit);

            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            return (company, contact, currency, user, steelGrade, unit);
        }

        private async Task<Product> SeedProductAsync(
            Currency currency,
            SteelGrade steelGrade,
            UnitOfMeasure unit,
            int stockQuantity,
            string name = "Rura Stalowa")
        {
            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = name,
                SteelGradeId = steelGrade.Id,
                UnitId = unit.Id,
                CurrencyId = currency.Id,
                PricePerUnit = 20000,
                StockQuantity = stockQuantity,
                Category = ProductCategoryEnum.Pipe
            };

            _contextMock.Products.Add(product);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            return product;
        }

        // ─── Handle ────────────────────────────────────────────────────────────

        [Test]
        public async Task Handle_WhenOfferNotFound_ThrowsInvalidOperationException()
        {
            // Arrange
            var nonExistentOfferId = Guid.NewGuid();
            var notification = new OfferAcceptedEvent(nonExistentOfferId);

            // Act & Assert
            await Assert.That(async () => await _handler.Handle(notification, CancellationToken.None))
                .Throws<InvalidOperationException>();

            var dealsCount = await _contextMock.Deals.CountAsync();
            await Assert.That(dealsCount).IsEqualTo(0);
        }

        [Test]
        public async Task Handle_WhenStockIsInsufficient_ThrowsBusinessRuleExceptionAndDoesNotCreateDeal()
        {
            // Arrange
            var (company, contact, currency, owner, steelGrade, unit) = await SeedBaseGraphAsync();

            var product = await SeedProductAsync(currency, steelGrade, unit, stockQuantity: 5, name: "Towar Ograniczony");

            var offer = new Offer
            {
                Id = Guid.NewGuid(),
                Name = "OF/2026/09/0001",
                ContactId = contact.Id,
                CreatedByUserId = owner.Id,
                CurrencyId = currency.Id,
                ValidUntil = DateTime.UtcNow.AddDays(7),
                Status = OfferStatusEnum.Sent
            };
            _contextMock.Offers.Add(offer);

            _contextMock.OfferProducts.Add(new OfferProducts
            {
                Id = Guid.NewGuid(),
                OfferId = offer.Id,
                ProductId = product.Id,
                Quantity = 10,
                QuotedPrice = 18000
            });

            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            var notification = new OfferAcceptedEvent(offer.Id);

            // Act & Assert
            await Assert.That(async () => await _handler.Handle(notification, CancellationToken.None))
                .Throws<BusinessRuleException>();

            var dealCreated = await _contextMock.Deals.AnyAsync(d => d.Name == $"SE/{offer.Name}");
            await Assert.That(dealCreated).IsFalse();
        }

        [Test]
        public async Task Handle_WhenValidOffer_CreatesDealWithProductsAndCorrectValues()
        {
            // Arrange
            var (company, contact, currency, owner, steelGrade, unit) = await SeedBaseGraphAsync();

            var productA = await SeedProductAsync(currency, steelGrade, unit, stockQuantity: 50, name: "Produkt A");
            var productB = await SeedProductAsync(currency, steelGrade, unit, stockQuantity: 30, name: "Produkt B");

            var offer = new Offer
            {
                Id = Guid.NewGuid(),
                Name = "OF/2026/09/SUCCESS",
                ContactId = contact.Id,
                CreatedByUserId = owner.Id,
                CurrencyId = currency.Id,
                ValidUntil = DateTime.UtcNow.AddDays(7),
                Status = OfferStatusEnum.Sent
            };
            _contextMock.Offers.Add(offer);

            _contextMock.OfferProducts.AddRange(
                new OfferProducts
                {
                    Id = Guid.NewGuid(),
                    OfferId = offer.Id,
                    ProductId = productA.Id,
                    Quantity = 2,
                    QuotedPrice = 25000
                },
                new OfferProducts
                {
                    Id = Guid.NewGuid(),
                    OfferId = offer.Id,
                    ProductId = productB.Id,
                    Quantity = 3,
                    QuotedPrice = 40000
                }
            );

            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            var expectedTotalValue = (2L * 25000) + (3L * 40000);
            var notification = new OfferAcceptedEvent(offer.Id);

            // Act
            await _handler.Handle(notification, CancellationToken.None);

            // Assert
            var createdDeal = await _contextMock.Deals
                .Include(d => d.DealProducts)
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Name == $"SE/{offer.Name}");

            await Assert.That(createdDeal).IsNotNull();
            await Assert.That(createdDeal!.Name).IsEqualTo($"SE/{offer.Name}");
            await Assert.That(createdDeal.Value).IsEqualTo(expectedTotalValue);
            await Assert.That(createdDeal.Status).IsEqualTo(DealsStatusEnum.ToDo);
            await Assert.That(createdDeal.CurrencyId).IsEqualTo(currency.Id);
            await Assert.That(createdDeal.OwnerId).IsEqualTo(owner.Id);
            await Assert.That(createdDeal.CompanyId).IsEqualTo(company.Id);
            await Assert.That(createdDeal.ContactId).IsEqualTo(contact.Id);
            await Assert.That(createdDeal.CloseDate).IsGreaterThan(DateTime.UtcNow.AddDays(25));

            await Assert.That(createdDeal.DealProducts.Count).IsEqualTo(2);

            var dealProdA = createdDeal.DealProducts.First(dp => dp.ProductId == productA.Id);
            await Assert.That(dealProdA.Quantity).IsEqualTo(2);
            await Assert.That(dealProdA.UnitPrice).IsEqualTo(25000L);

            var dealProdB = createdDeal.DealProducts.First(dp => dp.ProductId == productB.Id);
            await Assert.That(dealProdB.Quantity).IsEqualTo(3);
            await Assert.That(dealProdB.UnitPrice).IsEqualTo(40000L);
        }

        [Test]
        public async Task Handle_WhenOneOfMultipleProductsLacksStock_AbortsEntireTransaction()
        {
            // Arrange
            var (company, contact, currency, owner, steelGrade, unit) = await SeedBaseGraphAsync();

            var productAvailable = await SeedProductAsync(currency, steelGrade, unit, stockQuantity: 100, name: "Dostępny");
            var productShortage = await SeedProductAsync(currency, steelGrade, unit, stockQuantity: 2, name: "Brakujący");

            var offer = new Offer
            {
                Id = Guid.NewGuid(),
                Name = "OF/MULTI/SHORTAGE",
                ContactId = contact.Id,
                CreatedByUserId = owner.Id,
                CurrencyId = currency.Id,
                ValidUntil = DateTime.UtcNow.AddDays(7),
                Status = OfferStatusEnum.Sent
            };
            _contextMock.Offers.Add(offer);

            _contextMock.OfferProducts.AddRange(
                new OfferProducts
                {
                    Id = Guid.NewGuid(),
                    OfferId = offer.Id,
                    ProductId = productAvailable.Id,
                    Quantity = 5, 
                    QuotedPrice = 10000
                },
                new OfferProducts
                {
                    Id = Guid.NewGuid(),
                    OfferId = offer.Id,
                    ProductId = productShortage.Id,
                    Quantity = 10, 
                    QuotedPrice = 20000
                }
            );

            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            var notification = new OfferAcceptedEvent(offer.Id);

            // Act & Assert
            await Assert.That(async () => await _handler.Handle(notification, CancellationToken.None))
                .Throws<BusinessRuleException>();

            var dealExists = await _contextMock.Deals.AnyAsync(d => d.Name == $"SE/{offer.Name}");
            await Assert.That(dealExists).IsFalse();
        }
    }
}
