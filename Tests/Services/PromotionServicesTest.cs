using Domain.Constants;
using Domain.Enum;
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
    public class PromotionServicesTest
    {
        protected AppDbContext _contextMock = null!;
        protected PromotionServices _promotionServicesMock = null!;
        protected ILogger<PromotionServices> _loggerMock = null!;
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

            _loggerMock = new LoggerFactory().CreateLogger<PromotionServices>();

            _promotionServicesMock = new PromotionServices(_contextMock, _loggerMock);
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

            var steelGrade = new SteelGrade { 
                Id = Guid.NewGuid(), 
                Name = "S235" 
            };
            _contextMock.SteelGrades.Add(steelGrade);


            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Test Product",
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
                Unit = unit
            };
            _contextMock.Products.Add(product);
            await _contextMock.SaveChangesAsync();
            return product;
        }

        // ─── GetPromotionListAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetPromotionListAsync_WhenNoFiltersApplied_ReturnsAllPromotionsAndMapsProperly()
        {
            // Arrange
            var product = await CreateDummyProductAsync();

            var promo1 = new Promotion
            {
                Id = Guid.NewGuid(),
                Name = "Promo 1",
                DiscountPercentage = 10,
                PromotionalPrice = null,
                StartDate = new DateTime(2023, 1, 1).ToUniversalTime(),
                EndDate = new DateTime(2023, 12, 31).ToUniversalTime(),
                IsActive = true,
                ProductId = product.Id,
                Product = product
            };

            var promo2 = new Promotion
            {
                Id = Guid.NewGuid(),
                Name = "Promo 2",
                DiscountPercentage = null,
                PromotionalPrice = 50000,
                StartDate = null,
                EndDate = null,
                IsActive = false,
                ProductId = product.Id,
                Product = product
            };

            _contextMock.Promotions.AddRange(promo1, promo2);
            await _contextMock.SaveChangesAsync();

            var command = new PromotionListCommand
            {
                PageNumber = 1,
                PageSize = 10,
                IsActive = null
            };

            // Act
            var result = await _promotionServicesMock.GetPromotionListAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            var items = result.Data!.Items.ToList();

            await Assert.That(items).Count().IsEqualTo(2);

            var mappedPromo1 = items.First(p => p.Id == promo1.Id);
            await Assert.That(mappedPromo1.Name).IsEqualTo("Promo 1");
            await Assert.That(mappedPromo1.DiscountPercentage).IsEqualTo(10);
            await Assert.That(mappedPromo1.PromotionalPrice).IsNull();
            await Assert.That(mappedPromo1.IsActive).IsTrue();
        }

        [Test]
        public async Task GetPromotionListAsync_FiltersByIsActive()
        {
            // Arrange
            var product = await CreateDummyProductAsync();

            var activePromo = new Promotion { Id = Guid.NewGuid(), Name = "Active", IsActive = true, ProductId = product.Id, Product = product };
            var inactivePromo = new Promotion { Id = Guid.NewGuid(), Name = "Inactive", IsActive = false, ProductId = product.Id, Product = product };

            _contextMock.Promotions.AddRange(activePromo, inactivePromo);
            await _contextMock.SaveChangesAsync();

            var command = new PromotionListCommand { PageNumber = 1, PageSize = 10, IsActive = true };

            // Act
            var result = await _promotionServicesMock.GetPromotionListAsync(command);

            // Assert
            var items = result.Data!.Items.ToList();
            await Assert.That(items).Count().IsEqualTo(1);
            await Assert.That(items.First().Id).IsEqualTo(activePromo.Id);
        }

        [Test]
        public async Task GetPromotionListAsync_FiltersByDatesCorrectly()
        {
            // Arrange
            var product = await CreateDummyProductAsync();
            var mayFirst = new DateTime(2024, 5, 1).ToUniversalTime();
            var mayThirtyFirst = new DateTime(2024, 5, 31).ToUniversalTime();

            var promoInMay = new Promotion
            {
                Id = Guid.NewGuid(),
                Name = "May Promo",
                StartDate = mayFirst,
                EndDate = mayThirtyFirst,
                IsActive = true,
                ProductId = product.Id,
                Product = product
            };

            var promoInJune = new Promotion
            {
                Id = Guid.NewGuid(),
                Name = "June Promo",
                StartDate = new DateTime(2024, 6, 1).ToUniversalTime(),
                EndDate = new DateTime(2024, 6, 30).ToUniversalTime(),
                IsActive = true,
                ProductId = product.Id,
                Product = product
            };

            _contextMock.Promotions.AddRange(promoInMay, promoInJune);
            await _contextMock.SaveChangesAsync();

            var command = new PromotionListCommand
            {
                PageNumber = 1,
                PageSize = 10,
                IsActive = null,
                FromDate = new DateTime(2024, 4, 1).ToUniversalTime(),
                ToDate = new DateTime(2024, 5, 31).ToUniversalTime()
            }; ;

            // Act
            var result = await _promotionServicesMock.GetPromotionListAsync(command);

            // Assert
            var items = result.Data!.Items.ToList();
            await Assert.That(items).Count().IsEqualTo(1);
            await Assert.That(items.First().Name).IsEqualTo("May Promo");
        }

        [Test]
        public async Task GetPromotionListAsync_FiltersByDiscountAndPriceCorrectly()
        {
            // Arrange
            var product = await CreateDummyProductAsync();

            var promoHighDiscount = new Promotion
            {
                Id = Guid.NewGuid(),
                Name = "50% off",
                DiscountPercentage = 50,
                PromotionalPrice = null,
                IsActive = true,
                ProductId = product.Id,
                Product = product
            };

            var promoLowDiscount = new Promotion
            {
                Id = Guid.NewGuid(),
                Name = "10% off",
                DiscountPercentage = 10,
                PromotionalPrice = null,
                IsActive = true,
                ProductId = product.Id,
                Product = product
            };

            var promoFixedPrice = new Promotion
            {
                Id = Guid.NewGuid(),
                Name = "Fixed Price",
                DiscountPercentage = null,
                PromotionalPrice = 10000,
                IsActive = true,
                ProductId = product.Id,
                Product = product
            };

            _contextMock.Promotions.AddRange(promoHighDiscount, promoLowDiscount, promoFixedPrice);
            await _contextMock.SaveChangesAsync();

            // Act 1
            var commandDiscount = new PromotionListCommand { PageNumber = 1, PageSize = 10, IsActive = null, DiscountPrecentageFrom = 20, DiscountPrecentageTo = 100 };
            var resultDiscount = await _promotionServicesMock.GetPromotionListAsync(commandDiscount);

            // Assert 1
            await Assert.That(resultDiscount.Data!.Items).Count().IsEqualTo(1);
            await Assert.That(resultDiscount.Data!.Items.First().Name).IsEqualTo("50% off");

            // Act 2
            var commandPrice = new PromotionListCommand { PageNumber = 1, PageSize = 10, IsActive = null, PromotionPriceFrom = 5000, PromotionPriceTo = 20000 };
            var resultPrice = await _promotionServicesMock.GetPromotionListAsync(commandPrice);

            // Assert 2
            await Assert.That(resultPrice.Data!.Items).Count().IsEqualTo(1);
            await Assert.That(resultPrice.Data!.Items.First().Name).IsEqualTo("Fixed Price");
        }

        [Test]
        public async Task GetPromotionListAsync_AppliesSearchTermIgnoringAccentsAndCase()
        {
            // Arrange
            var product = await CreateDummyProductAsync();

            var promo1 = new Promotion
            {
                Id = Guid.NewGuid(),
                Name = "Wiosenna Zniżka",
                IsActive = true,
                ProductId = product.Id,
                Product = product
            };

            var promo2 = new Promotion
            {
                Id = Guid.NewGuid(),
                Name = "Letnia Wyprzedaż",
                IsActive = true,
                ProductId = product.Id,
                Product = product
            };

            _contextMock.Promotions.AddRange(promo1, promo2);
            await _contextMock.SaveChangesAsync();

            var command = new PromotionListCommand { PageNumber = 1, PageSize = 10, IsActive = null, SearchTerm = "znizka" };

            // Act
            var result = await _promotionServicesMock.GetPromotionListAsync(command);

            // Assert
            var items = result.Data!.Items.ToList();
            await Assert.That(items).Count().IsEqualTo(1);
            await Assert.That(items.First().Name).IsEqualTo("Wiosenna Zniżka");
        }

        [Test]
        public async Task GetPromotionListAsync_AppliesSortingCorrectly()
        {
            // Arrange
            var product = await CreateDummyProductAsync();

            var promo1 = new Promotion
            {
                Id = Guid.NewGuid(),
                Name = "A Promo",
                DiscountPercentage = 10,
                IsActive = true,
                ProductId = product.Id,
                Product = product
            };

            var promo2 = new Promotion
            {
                Id = Guid.NewGuid(),
                Name = "B Promo",
                DiscountPercentage = 50,
                IsActive = true,
                ProductId = product.Id,
                Product = product
            };

            var promo3 = new Promotion
            {
                Id = Guid.NewGuid(),
                Name = "C Promo",
                DiscountPercentage = 30,
                IsActive = true,
                ProductId = product.Id,
                Product = product
            };

            _contextMock.Promotions.AddRange(promo1, promo2, promo3);
            await _contextMock.SaveChangesAsync();

            var command = new PromotionListCommand
            {
                PageNumber = 1,
                PageSize = 10,
                IsActive = null,
                SortBy = "discountPercentage",
                SortDescending = true
            };

            // Act
            var result = await _promotionServicesMock.GetPromotionListAsync(command);

            // Assert
            var items = result.Data!.Items.ToList();
            await Assert.That(items).Count().IsEqualTo(3);
            await Assert.That(items[0].Name).IsEqualTo("B Promo");
            await Assert.That(items[1].Name).IsEqualTo("C Promo");
            await Assert.That(items[2].Name).IsEqualTo("A Promo");
        }

        // ─── GetPromotionDetailAsync ───────────────────────────────────────────────

        [Test]
        public async Task GetPromotionDetailAsync_WhenPromotionExistsWithFullData_ReturnsSuccessWithMappedDetails()
        {
            // Arrange
            var unit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = "Metr bieżący",
                Symbol = "mb",
                BaseMultiplier = 1
            };
            _contextMock.UnitsOfMeasure.Add(unit);

            var steelGrade = new SteelGrade
            {
                Id = Guid.NewGuid(),
                Name = "S355J2H"
            };

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Rura Stalowa Precyzyjna",
                SteelGradeId = steelGrade.Id,
                SteelGrade = steelGrade,
                Thickness = 5,
                Width = 0,
                Length = 6000,
                Diameter = 60,
                Weight = 15000,
                PricePerUnit = 250000,
                StockQuantity = 120,
                Category = ProductCategoryEnum.Pipe,
                UnitId = unit.Id
            };
            _contextMock.Products.Add(product);

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Polski Złoty",
                Code = "PLN",
                DecimalPlaces = 2
            };
            _contextMock.Currencies.Add(currency);

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
            _contextMock.Users.Add(owner);

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = "Budimex SA",
                NIP = "1234567890",
                OwnerId = owner.Id
            };
            _contextMock.Companies.Add(company);

            await _contextMock.SaveChangesAsync();

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Jan",
                LastName = "Kowalski",
                IsPrimary = true,
                CompanyId = company.Id,
                OwnerId = owner.Id,
                Owner = owner
            };
            _contextMock.Contacts.Add(contact);
            await _contextMock.SaveChangesAsync();

            var promotion = new Promotion
            {
                Id = Guid.NewGuid(),
                Name = "Mega Promocja Rury",
                IsActive = true,
                StartDate = new DateTime(2026, 1, 1).ToUniversalTime(),
                EndDate = new DateTime(2026, 12, 31).ToUniversalTime(),
                DiscountPercentage = 15.5m,
                PromotionalPrice = 211250,
                CurrencyId = currency.Id,
                MinQuantity = 10,
                MinWeight = 150000,
                ProductId = product.Id,
                ContactId = contact.Id
            };
            _contextMock.Promotions.Add(promotion);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _promotionServicesMock.GetPromotionDetailAsync(promotion.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            var detail = result.Data!;

            await Assert.That(detail.Id).IsEqualTo(promotion.Id);
            await Assert.That(detail.Name).IsEqualTo("Mega Promocja Rury");
            await Assert.That(detail.IsActive).IsTrue();
            await Assert.That(detail.DiscountPercentage).IsEqualTo(15.5m);
            await Assert.That(detail.PromotionalPrice).IsEqualTo(211250);
            await Assert.That(detail.CurrencyCode).IsEqualTo("PLN");
            await Assert.That(detail.CurrencyDecimalPlaces).IsEqualTo(2);
            await Assert.That(detail.MinQuantity).IsEqualTo(10);
            await Assert.That(detail.MinWeight).IsEqualTo(150000);
            await Assert.That(detail.ProductId).IsEqualTo(product.Id);
            await Assert.That(detail.ProductName).IsEqualTo("Rura Stalowa Precyzyjna");
            await Assert.That(detail.SteelGrade).IsEqualTo("S355J2H");
            await Assert.That(detail.Category).IsEqualTo(ProductCategoryEnum.Pipe.ToString());
            await Assert.That(detail.ProductPricePerUnit).IsEqualTo(250000);
            await Assert.That(detail.ProductStockQuantity).IsEqualTo(120);
            await Assert.That(detail.UnitSymbol).IsEqualTo("mb");
            await Assert.That(detail.Dimensions).IsNotEmpty();
            await Assert.That(detail.ContactId).IsEqualTo(contact.Id);
            await Assert.That(detail.ContactFirstName).IsEqualTo("Jan");
            await Assert.That(detail.ContactLastName).IsEqualTo("Kowalski");
            await Assert.That(detail.ContactCompanyName).IsEqualTo("Budimex SA");
        }

        [Test]
        public async Task GetPromotionDetailAsync_WhenOptionalFieldsAreNull_ReturnsSuccessWithNulls()
        {
            // Arrange
            var product = await CreateDummyProductAsync();

            var promotion = new Promotion
            {
                Id = Guid.NewGuid(),
                Name = "Ogólna Promocja",
                IsActive = true,
                StartDate = null,
                EndDate = null,
                DiscountPercentage = null,
                PromotionalPrice = null,
                CurrencyId = null,
                Currency = null,
                MinQuantity = null,
                MinWeight = null,
                ProductId = product.Id,
                Product = product,
                ContactId = null,
                Contact = null
            };
            _contextMock.Promotions.Add(promotion);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _promotionServicesMock.GetPromotionDetailAsync(promotion.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            var detail = result.Data!;

            await Assert.That(detail.Id).IsEqualTo(promotion.Id);
            await Assert.That(detail.CurrencyCode).IsNull();
            await Assert.That(detail.CurrencyDecimalPlaces).IsNull();
            await Assert.That(detail.ContactId).IsNull();
            await Assert.That(detail.ContactFirstName).IsNull();
            await Assert.That(detail.ContactLastName).IsNull();
            await Assert.That(detail.ContactCompanyName).IsNull();
            await Assert.That(detail.StartDate).IsNull();
            await Assert.That(detail.EndDate).IsNull();
        }

        [Test]
        public async Task GetPromotionDetailAsync_WhenPromotionDoesNotExist_ReturnsFailureNotFound()
        {
            // Arrange
            var nonExistingId = Guid.NewGuid();

            // Act
            var result = await _promotionServicesMock.GetPromotionDetailAsync(nonExistingId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(404);
            await Assert.That(result.ErrorCode).IsEqualTo(Domain.Constants.ErrorCodes.PromotionNotFound);
        }

        // ─── DeactivatePromotionAsync ───────────────────────────────────────────────
        [Test]
        public async Task DeactivatePromotionAsync_WhenPromotionExistsAndIsActive_Return200()
        {
            // Arrange
            var product = await CreateDummyProductAsync();
            var promotion = new Promotion
            {
                Id = Guid.NewGuid(),
                Name = "aaaaaaaadaadada",
                IsActive = true,
                ProductId = product.Id,
                Product = product
            };

            _contextMock.Promotions.Add(promotion);

            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _promotionServicesMock.DeactivatePromotionAsync(promotion.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(200);

            var updatedPromotion = await _contextMock.Promotions.FindAsync(promotion.Id);

            await Assert.That(updatedPromotion!.IsActive).IsFalse();
        }


        [Test]
        public async Task DeactivatePromotionAsync_WhenPromotionIsAlreadyDeactivate_Return200()
        {
            // Arrange
            var product = await CreateDummyProductAsync();
            var promotion = new Promotion
            {
                Id = Guid.NewGuid(),
                Name = "darereredaadada",
                IsActive = false,
                ProductId = product.Id,
                Product = product
            };

            _contextMock.Promotions.Add(promotion);

            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _promotionServicesMock.DeactivatePromotionAsync(promotion.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(200);
        }

        [Test]
        public async Task DeactivatePromotionAsync_WhenPromotionDoesntExist_Return404()
        {
            // Act
            var result = await _promotionServicesMock.DeactivatePromotionAsync(Guid.NewGuid());

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(404);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.PromotionNotFound);
        }

        // ─── ActivatePromotionAsync ─────────────────────────────────────────────────

        [Test]
        public async Task ActivatePromotionAsync_WhenPromotionExistsAndIsInactive_ActivatesPromotionAndSetsDates()
        {
            // Arrange
            var product = await CreateDummyProductAsync();
            var promotion = new Promotion
            {
                Id = Guid.NewGuid(),
                Name = "Wznowiona Promocja",
                IsActive = false,
                StartDate = DateTime.UtcNow.AddMonths(-2),
                EndDate = DateTime.UtcNow.AddMonths(-1),
                ProductId = product.Id,
                Product = product
            };

            _contextMock.Promotions.Add(promotion);
            await _contextMock.SaveChangesAsync();

            var newEndDate = DateTime.UtcNow.AddMonths(1);
            var command = new ActivatePromotionCommand
            {
                Id = promotion.Id,
                EndDate = newEndDate
            };

            // Act
            var result = await _promotionServicesMock.ActivatePromotionAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(200);

            var updatedPromotion = await _contextMock.Promotions.FindAsync(promotion.Id);
            await Assert.That(updatedPromotion!.IsActive).IsTrue();
            await Assert.That(updatedPromotion.EndDate).IsNotNull();
            await Assert.That(updatedPromotion.StartDate).IsNotNull();
        }

        [Test]
        public async Task ActivatePromotionAsync_WhenPromotionIsAlreadyActive_ReturnsSuccess200()
        {
            // Arrange
            var product = await CreateDummyProductAsync();
            var promotion = new Promotion
            {
                Id = Guid.NewGuid(),
                Name = "Ciągle Aktywna Promocja",
                IsActive = true,
                StartDate = DateTime.UtcNow.AddDays(-5),
                EndDate = DateTime.UtcNow.AddDays(10),
                ProductId = product.Id,
                Product = product
            };

            _contextMock.Promotions.Add(promotion);
            await _contextMock.SaveChangesAsync();

            var command = new ActivatePromotionCommand
            {
                Id = promotion.Id,
                EndDate = DateTime.UtcNow.AddMonths(2)
            };

            // Act
            var result = await _promotionServicesMock.ActivatePromotionAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(200);
            await Assert.That(result.Message).IsEqualTo("Promotion is already activated.");
        }

        [Test]
        public async Task ActivatePromotionAsync_WhenAnotherActivePromotionExistsForSameProduct_Returns409Conflict()
        {
            // Arrange
            var product = await CreateDummyProductAsync();

            var activePromo = new Promotion
            {
                Id = Guid.NewGuid(),
                Name = "Trwająca Promocja",
                IsActive = true,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow.AddDays(20),
                ProductId = product.Id,
                Product = product
            };

            var inactivePromo = new Promotion
            {
                Id = Guid.NewGuid(),
                Name = "Próba Wznowienia",
                IsActive = false,
                ProductId = product.Id,
                Product = product
            };

            _contextMock.Promotions.AddRange(activePromo, inactivePromo);
            await _contextMock.SaveChangesAsync();

            var command = new ActivatePromotionCommand
            {
                Id = inactivePromo.Id,
                EndDate = DateTime.UtcNow.AddDays(15)
            };

            // Act
            var result = await _promotionServicesMock.ActivatePromotionAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(409);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.ActivePromotionAlreadyExists);
        }

        [Test]
        public async Task ActivatePromotionAsync_WhenPromotionDoesNotExist_Returns404NotFound()
        {
            // Arrange
            var command = new ActivatePromotionCommand
            {
                Id = Guid.NewGuid(),
                EndDate = DateTime.UtcNow.AddMonths(1)
            };

            // Act
            var result = await _promotionServicesMock.ActivatePromotionAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(404);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.PromotionNotFound);
        }

        // ─── DeletePromotionAsync ───────────────────────────────────────────────────

        [Test]
        public async Task DeletePromotionAsync_WhenPromotionExists_SoftDeletesPromotionAndHidesItFromQueries()
        {
            // Arrange
            var product = await CreateDummyProductAsync();
            var promotion = new Promotion
            {
                Id = Guid.NewGuid(),
                Name = "Promocja do usunięcia",
                IsActive = true,
                ProductId = product.Id,
                Product = product
            };

            _contextMock.Promotions.Add(promotion);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _promotionServicesMock.DeletePromotionAsync(promotion.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(200);

            var hiddenPromotion = await _contextMock.Promotions.FirstOrDefaultAsync(p => p.Id == promotion.Id);
            await Assert.That(hiddenPromotion).IsNull();

            var softDeletedPromotion = await _contextMock.Promotions
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == promotion.Id);

            await Assert.That(softDeletedPromotion).IsNotNull();
            await Assert.That(softDeletedPromotion!.IsDeleted).IsTrue();
            await Assert.That(softDeletedPromotion.UpdateAt).IsNotNull();
        }

        [Test]
        public async Task DeletePromotionAsync_WhenPromotionDoesNotExist_Returns404NotFound()
        {
            // Arrange
            var nonExistingId = Guid.NewGuid();

            // Act
            var result = await _promotionServicesMock.DeletePromotionAsync(nonExistingId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(404);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.PromotionNotFound);
        }

        // ─── EditPromotionAsync ───────────────────────────────────────────────────

        [Test]
        public async Task EditPromotionAsync_WhenUpdatingOnlyBasicFields_UpdatesOnlySpecifiedFields()
        {
            // Arrange
            var product = await CreateDummyProductAsync();
            var promotion = new Promotion
            {
                Id = Guid.NewGuid(),
                Name = "Stara Nazwa",
                DiscountPercentage = 15,
                MinQuantity = 10,
                MinWeight = 20000,
                IsActive = true,
                ProductId = product.Id,
                Product = product
            };

            _contextMock.Promotions.Add(promotion);
            await _contextMock.SaveChangesAsync();

            var command = new EditPromotionCommand
            {
                Id = promotion.Id,
                Name = "Nowa Poprawiona Nazwa",
                MinQuantity = 25
            };

            // Act
            var result = await _promotionServicesMock.EditPromotionAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(200);

            var updated = await _contextMock.Promotions.FindAsync(promotion.Id);
            await Assert.That(updated!.Name).IsEqualTo("Nowa Poprawiona Nazwa");
            await Assert.That(updated.MinQuantity).IsEqualTo(25);
            await Assert.That(updated.DiscountPercentage).IsEqualTo(15);
            await Assert.That(updated.MinWeight).IsEqualTo(20000);
        }

        [Test]
        public async Task EditPromotionAsync_WhenSwitchingToPromotionalPrice_ClearsDiscountPercentageAndSetsCurrency()
        {
            // Arrange
            var product = await CreateDummyProductAsync();

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Polski Złoty",
                Code = "PLN",
                DecimalPlaces = 2
            };

            _contextMock.Currencies.Add(currency);

            var promotion = new Promotion
            {
                Id = Guid.NewGuid(),
                Name = "Promocja Procentowa",
                DiscountPercentage = 20,
                PromotionalPrice = null,
                CurrencyId = null,
                IsActive = true,
                ProductId = product.Id,
                Product = product
            };

            _contextMock.Promotions.Add(promotion);
            await _contextMock.SaveChangesAsync();

            var command = new EditPromotionCommand
            {
                Id = promotion.Id,
                PromotionalPrice = 85000,
                CurrencyId = currency.Id
            };

            // Act
            var result = await _promotionServicesMock.EditPromotionAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();

            var updated = await _contextMock.Promotions.FindAsync(promotion.Id);
            await Assert.That(updated!.PromotionalPrice).IsEqualTo(85000);
            await Assert.That(updated.CurrencyId).IsEqualTo(currency.Id);
            await Assert.That(updated.DiscountPercentage).IsNull();
        }

        [Test]
        public async Task EditPromotionAsync_WhenSwitchingToDiscountPercentage_ClearsPromotionalPriceAndCurrency()
        {
            // Arrange
            var product = await CreateDummyProductAsync();

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Polski Złoty",
                Code = "PLN",
                DecimalPlaces = 2
            };
            _contextMock.Currencies.Add(currency);

            var promotion = new Promotion
            {
                Id = Guid.NewGuid(),
                Name = "Promocja Sztywnej Ceny",
                DiscountPercentage = null,
                PromotionalPrice = 50000,
                CurrencyId = currency.Id,
                IsActive = true,
                ProductId = product.Id,
                Product = product
            };

            _contextMock.Promotions.Add(promotion);
            await _contextMock.SaveChangesAsync();

            var command = new EditPromotionCommand
            {
                Id = promotion.Id,
                DiscountPercentage = 30
            };

            // Act
            var result = await _promotionServicesMock.EditPromotionAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();

            var updated = await _contextMock.Promotions.FindAsync(promotion.Id);
            await Assert.That(updated!.DiscountPercentage).IsEqualTo(30);
            await Assert.That(updated.PromotionalPrice).IsNull();
            await Assert.That(updated.CurrencyId).IsNull();
        }

        [Test]
        public async Task EditPromotionAsync_WhenEndDateIsBeforeStartDate_Returns400BadRequest()
        {
            // Arrange
            var product = await CreateDummyProductAsync();
            var startDate = new DateTime(2026, 6, 1).ToUniversalTime();

            var promotion = new Promotion
            {
                Id = Guid.NewGuid(),
                Name = "Promocja z datami",
                StartDate = startDate,
                EndDate = new DateTime(2026, 6, 30).ToUniversalTime(),
                IsActive = true,
                ProductId = product.Id,
                Product = product
            };

            _contextMock.Promotions.Add(promotion);
            await _contextMock.SaveChangesAsync();

            var command = new EditPromotionCommand
            {
                Id = promotion.Id,
                EndDate = startDate.AddDays(-5)
            };

            // Act
            var result = await _promotionServicesMock.EditPromotionAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(400);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidDate);
        }

        [Test]
        public async Task EditPromotionAsync_WhenCurrencyDoesNotExist_Returns400BadRequest()
        {
            // Arrange
            var product = await CreateDummyProductAsync();
            var promotion = new Promotion
            {
                Id = Guid.NewGuid(),
                Name = "Promocja",
                IsActive = true,
                ProductId = product.Id,
                Product = product
            };

            _contextMock.Promotions.Add(promotion);
            await _contextMock.SaveChangesAsync();

            var command = new EditPromotionCommand
            {
                Id = promotion.Id,
                PromotionalPrice = 10000,
                CurrencyId = Guid.NewGuid()
            };

            // Act
            var result = await _promotionServicesMock.EditPromotionAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(400);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.CurrencyNotFound);
        }

        [Test]
        public async Task EditPromotionAsync_WhenContactDoesNotExist_Returns400BadRequest()
        {
            // Arrange
            var product = await CreateDummyProductAsync();
            var promotion = new Promotion
            {
                Id = Guid.NewGuid(),
                Name = "Promocja",
                IsActive = true,
                ProductId = product.Id,
                Product = product
            };

            _contextMock.Promotions.Add(promotion);
            await _contextMock.SaveChangesAsync();

            var command = new EditPromotionCommand
            {
                Id = promotion.Id,
                ContactId = Guid.NewGuid()
            };

            // Act
            var result = await _promotionServicesMock.EditPromotionAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(400);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.ContactNotFound);
        }

        [Test]
        public async Task EditPromotionAsync_WhenPromotionDoesNotExist_Returns404NotFound()
        {
            // Arrange
            var command = new EditPromotionCommand
            {
                Id = Guid.NewGuid(),
                Name = "Nieistniejąca"
            };

            // Act
            var result = await _promotionServicesMock.EditPromotionAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(404);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.PromotionNotFound);
        }


        // ─── AddPromotionAsync ────────────────────────────────────────────────────

        [Test]
        public async Task AddPromotionAsync_WhenDataIsValid_CreatesPromotionAndReturnsCreated()
        {
            // Arrange
            var product = await CreateDummyProductAsync();
            var command = new AddPromotionCommand
            {
                Name = "Letnia Promocja 2026",
                ProductId = product.Id,
                DiscountPercentage = 20,
                MinQuantity = 5,
                MinWeight = 10000
            };

            // Act
            var result = await _promotionServicesMock.AddPromotionAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status201Created);

            var createdPromotion = await _contextMock.Promotions.FirstOrDefaultAsync(p => p.ProductId == product.Id);
            await Assert.That(createdPromotion).IsNotNull();
            await Assert.That(createdPromotion!.Name).IsEqualTo("Letnia Promocja 2026");
            await Assert.That(createdPromotion.DiscountPercentage).IsEqualTo(20);
            await Assert.That(createdPromotion.IsActive).IsTrue();
        }

        [Test]
        public async Task AddPromotionAsync_WhenProductDoesNotExist_Returns404NotFound()
        {
            // Arrange
            var command = new AddPromotionCommand
            {
                Name = "Promocja",
                ProductId = Guid.NewGuid(),
                DiscountPercentage = 10
            };

            // Act
            var result = await _promotionServicesMock.AddPromotionAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.ProductNotFound);
        }

        [Test]
        public async Task AddPromotionAsync_WhenAnotherActivePromotionAlreadyExistsForProduct_Returns409Conflict()
        {
            // Arrange
            var product = await CreateDummyProductAsync();
            var existingPromo = new Promotion
            {
                Id = Guid.NewGuid(),
                Name = "Istniejąca Promocja",
                ProductId = product.Id,
                IsActive = true,
                EndDate = DateTime.UtcNow.AddMonths(1)
            };
            _contextMock.Promotions.Add(existingPromo);
            await _contextMock.SaveChangesAsync();

            var command = new AddPromotionCommand
            {
                Name = "Nowa Nakładająca Się Promocja",
                ProductId = product.Id,
                DiscountPercentage = 15
            };

            // Act
            var result = await _promotionServicesMock.AddPromotionAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status409Conflict);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.ActivePromotionAlreadyExists);
        }

        [Test]
        public async Task AddPromotionAsync_WhenCurrencyDoesNotExist_Returns400BadRequest()
        {
            // Arrange
            var product = await CreateDummyProductAsync();
            var command = new AddPromotionCommand
            {
                Name = "Promocja Sztywna Cena",
                ProductId = product.Id,
                PromotionalPrice = 50000,
                CurrencyId = Guid.NewGuid()
            };

            // Act
            var result = await _promotionServicesMock.AddPromotionAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.CurrencyNotFound);
        }

        [Test]
        public async Task AddPromotionAsync_WhenContactDoesNotExist_Returns400BadRequest()
        {
            // Arrange
            var product = await CreateDummyProductAsync();
            var command = new AddPromotionCommand
            {
                Name = "Dedykowana Promocja",
                ProductId = product.Id,
                DiscountPercentage = 10,
                ContactId = Guid.NewGuid()
            };

            // Act
            var result = await _promotionServicesMock.AddPromotionAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.ContactNotFound);
        }
    }
}
