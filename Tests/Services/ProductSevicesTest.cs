using Domain.Constants;
using Domain.Enum;
using Domain.Models;
using Infrastructure;
using Infrastructure.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Services.Command.List;
using Services.Command.Product;
using Services.Helpers;
using Services.Services;
using Testcontainers.PostgreSql;

namespace Tests.Services
{
    public class ProductSevicesTest
    {
        protected AppDbContext _contextMock = null!;

        private static PostgreSqlContainer _dbContainer = null!;
        private static string _connectionString = null!;

        protected ProductSevices _productSevicesMock = null!;
        protected ILogger<ProductSevices> _loggerMock = null!;

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

            _loggerMock = new LoggerFactory().CreateLogger<ProductSevices>();
            _productSevicesMock = new ProductSevices(_contextMock, _loggerMock);
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

        private SteelGrade CreateDummySteelGrade(string? name = "S235")
        {
            var steelGrade = new SteelGrade
            {
                Id = Guid.NewGuid(),
                Name = name ?? $"S235_{Guid.NewGuid():N}",
            };
            _contextMock.SteelGrades.Add(steelGrade);
            return steelGrade;
        }

        private Currency CreateDummyCurrency(string? name = "Złoty", string? code = "PLN")
        {
            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = name ?? $"Złoty_{Guid.NewGuid():N}",
                Code = code ?? $"PLN_{Guid.NewGuid():N}",
                DecimalPlaces = 2
            };
            _contextMock.Currencies.Add(currency);
            return currency;
        }

        // ─── GetProductListAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetProductListAsync_WhenNoProductsMatch_ReturnsEmptyListWithSuccessStatus()
        {
            // Arrange
            var command = new ProductListCommand
            {
                PageNumber = 1,
                PageSize = 10,
                SearchTerm = Guid.NewGuid().ToString()
            };

            // Act
            var result = await _productSevicesMock.GetProductListAsync(command);
            Console.WriteLine(string.Join(", ", result.Errors ?? new List<string>()));
            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("No products found.");
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.Items).IsEmpty();
        }

        [Test]
        public async Task GetProductListAsync_MapsRelationsAndDimensionsCorrectly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var unit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = "Sztuka",
                Symbol = "szt"
            };

            var steelGrade = CreateDummySteelGrade();
            var currency = CreateDummyCurrency();

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = $"Rura Czarna_{uniqueSuffix}",
                SteelGrade = steelGrade,
                SteelGradeId = steelGrade.Id,
                Thickness = 2,
                Width = 0,
                Length = 6000,
                Diameter = 50,
                Weight = 15,
                PricePerUnit = 500000,
                StockQuantity = 100,
                UnitId = unit.Id,
                Unit = unit,
                Category = ProductCategoryEnum.Other,
                CurrencyId = currency.Id,
                Currency = currency
            };

            _contextMock.UnitsOfMeasure.Add(unit);
            _contextMock.Products.Add(product);
            await _contextMock.SaveChangesAsync();

            var command = new ProductListCommand
            {
                PageNumber = 1,
                PageSize = 100,
                SearchTerm = uniqueSuffix
            };

            // Act
            var result = await _productSevicesMock.GetProductListAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var mappedProduct = result.Data!.Items.FirstOrDefault(p => p.Id == product.Id);

            await Assert.That(mappedProduct).IsNotNull();
            await Assert.That(mappedProduct!.Name).IsEqualTo(product.Name);
            await Assert.That(mappedProduct.SteelGrade).IsEqualTo("S235");
            await Assert.That(mappedProduct.StockQuantity).IsEqualTo(100);
            await Assert.That(mappedProduct.Category).IsEqualTo(ProductCategoryEnum.Other.ToString());
            await Assert.That(mappedProduct.UnitSymbol).IsEqualTo("szt");
            await Assert.That(mappedProduct.Dimensions).IsNotNull();
            await Assert.That(mappedProduct.Dimensions).IsNotEmpty();
        }

        [Test]
        public async Task GetProductListAsync_WhenSearchTermProvided_FiltersResultsCorrectly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var unit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = "Metr",
                Symbol = "m"
            };

            var steelGrade = CreateDummySteelGrade();
            var currency = CreateDummyCurrency();

            var targetProduct = new Product
            {
                Id = Guid.NewGuid(),
                Name = $"Profil Zamknięty_{uniqueSuffix}",
                SteelGrade = steelGrade,
                SteelGradeId = steelGrade.Id,
                Thickness = 3,
                Width = 40,
                Length = 6000,
                Weight = 20,
                PricePerUnit = 100,
                StockQuantity = 50,
                UnitId = unit.Id,
                Unit = unit,
                Category = ProductCategoryEnum.Profile,
                CurrencyId = currency.Id,
                Currency = currency
            };

            var otherProduct = new Product
            {
                Id = Guid.NewGuid(),
                Name = $"Blacha_{uniqueSuffix}",
                SteelGrade = steelGrade,
                SteelGradeId = steelGrade.Id,
                Thickness = 5,
                Width = 1000,
                Length = 2000,
                Weight = 100,
                PricePerUnit = 200,
                StockQuantity = 10,
                UnitId = unit.Id,
                Unit = unit,
                Category = ProductCategoryEnum.Other,
                CurrencyId = currency.Id,
                Currency = currency
            };

            _contextMock.UnitsOfMeasure.Add(unit);
            _contextMock.Products.AddRange(targetProduct, otherProduct);
            await _contextMock.SaveChangesAsync();

            var command = new ProductListCommand
            {
                PageNumber = 1,
                PageSize = 10,
                SearchTerm = "zamknięty"
            };

            // Act
            var result = await _productSevicesMock.GetProductListAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            var items = result.Data!.Items;
            await Assert.That(items.Any(p => p.Id == targetProduct.Id)).IsTrue();
            await Assert.That(items.Any(p => p.Id == otherProduct.Id)).IsFalse();
        }

        [Test]
        public async Task GetProductListAsync_AppliesFiltersAndSortingCorrectly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var unit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = "Sztuka",
                Symbol = "szt"
            };

            var steelGrade = CreateDummySteelGrade();
            var currency = CreateDummyCurrency();

            var p1 = new Product
            {
                Id = Guid.NewGuid(),
                Name = $"A_Rura_{uniqueSuffix}",
                SteelGrade = steelGrade,
                SteelGradeId = steelGrade.Id,
                Thickness = 2,
                Width = 0,
                Length = 6000,
                StockQuantity = 50,
                UnitId = unit.Id,
                Unit = unit,
                Category = ProductCategoryEnum.Other,
                CurrencyId = currency.Id,
                Currency = currency
            };

            var p2 = new Product
            {
                Id = Guid.NewGuid(),
                Name = $"B_Rura_{uniqueSuffix}",
                SteelGrade = steelGrade,
                SteelGradeId = steelGrade.Id,
                Thickness = 5,
                Width = 0,
                Length = 6000,
                StockQuantity = 10,
                UnitId = unit.Id,
                Unit = unit,
                Category = ProductCategoryEnum.Other,
                CurrencyId = currency.Id,
                Currency = currency
            };

            var p3 = new Product
            {
                Id = Guid.NewGuid(),
                Name = $"C_Profil_{uniqueSuffix}",
                SteelGrade = steelGrade,
                SteelGradeId = steelGrade.Id,
                Thickness = 2,
                Width = 20,
                Length = 6000,
                StockQuantity = 100,
                UnitId = unit.Id,
                Unit = unit,
                Category = ProductCategoryEnum.Profile,
                CurrencyId = currency.Id,
                Currency = currency
            };

            _contextMock.UnitsOfMeasure.Add(unit);
            _contextMock.Products.AddRange(p1, p2, p3);
            await _contextMock.SaveChangesAsync();

            var command = new ProductListCommand
            {
                PageNumber = 1,
                PageSize = 10,
                ProductCategory = ProductCategoryEnum.Other.ToString(),
                SortBy = "quantity",
                SortDescending = true,
                SearchTerm = uniqueSuffix
            };

            // Act
            var result = await _productSevicesMock.GetProductListAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            var items = result.Data!.Items;

            await Assert.That(items).Count().IsEqualTo(2);
            await Assert.That(items[0].Id).IsEqualTo(p1.Id);
            await Assert.That(items[1].Id).IsEqualTo(p2.Id);
        }

        // ─── GetProductCategoryAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetProductCategoryAsync_ReturnsAllEnumValues()
        {
            // Act
            var result = await _productSevicesMock.GetProductCategoryAsync();

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var expectedCategories = Enum.GetNames(typeof(ProductCategoryEnum)).ToList();
            var returnedCategories = result.Data!.ToList();

            await Assert.That(returnedCategories).Count().IsEqualTo(expectedCategories.Count);

            foreach (var category in expectedCategories)
            {
                await Assert.That(returnedCategories.Contains(category)).IsTrue();
            }
        }

        [Test]
        public async Task GetProductCategoryAsync_WhenCalled_ReturnsSuccessStatusAndNotNullData()
        {
            // Act
            var result = await _productSevicesMock.GetProductCategoryAsync();

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();
        }


        // ───  GetProductDetailsAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetProductDetailsAsync_WhenProductExist_Return200()
        {
            // Arrange
            var unit = new UnitOfMeasure
            {
                Name = "Ton",
                Symbol = "t",
                BaseMultiplier = 4
            };

            var productId = Guid.NewGuid();

            var steelGrade = CreateDummySteelGrade("SRT345");
            var currency = CreateDummyCurrency();

            var product = new Product
            {
                Id = productId,
                Name = "Test",
                SteelGrade = steelGrade,
                SteelGradeId = steelGrade.Id,
                Thickness = 20,
                Width = 30,
                Length = 40,
                Weight = 40000000,
                Unit = unit,
                PricePerUnit = 400000,
                StockQuantity = 100000,
                Category = ProductCategoryEnum.Other,
                CurrencyId = currency.Id,
                Currency = currency
            };

            _contextMock.UnitsOfMeasure.Add(unit);
            _contextMock.Products.Add(product);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _productSevicesMock.GetProductDetailsAsync(product.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Message).IsEqualTo("Product details retrieved successfully");
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var response = result.Data;

            string theoryDimmension = DimensionsFormatter.Format(
                 product.Category,
                 product.Diameter,
                 product.Thickness,
                 product.Width,
                 product.Length
             );

            await Assert.That(response!.Dimensions).IsEquatableTo(theoryDimmension);

            await Assert.That(response!.Id).IsEquatableTo(productId);
            await Assert.That(response!.Name).IsEquatableTo("Test");
            await Assert.That(response!.SteelGrade).IsEquatableTo("SRT345");
            await Assert.That(response!.Category).IsEquatableTo(ProductCategoryEnum.Other.ToString());
            await Assert.That(response!.StockQuantity).IsEquatableTo(100000);
            await Assert.That(response!.UnitSymbol).IsEquatableTo("t");
            await Assert.That(response!.PricePerUnit).IsEquatableTo(400000m);
            await Assert.That(response!.Weight).IsEquatableTo(40000000m);
            await Assert.That(response!.ReservedQuantity).IsEquatableTo(0);
        }

        [Test]
        public async Task GetProductDetailsAsync_WhenProductDoesNotExist_Return404()
        {
            // Act
            var result = await _productSevicesMock.GetProductDetailsAsync(Guid.NewGuid());

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.Data).IsNull();
            await Assert.That(result.Message).IsEqualTo("Product not found.");
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
        }

        // ─── GetMailingProductsAsync ───────────────────────────────────────────

        [Test]
        public async Task GetMailingProductsAsync_MapsPropertiesAndDimensionsCorrectly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var unit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = "Sztuka",
                Symbol = "szt"
            };

            var steelGrade = CreateDummySteelGrade("S355");
            var currency = CreateDummyCurrency();

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = $"Blacha Mailingowa_{uniqueSuffix}",
                SteelGrade = steelGrade,
                SteelGradeId = steelGrade.Id,
                Thickness = 10,
                Width = 1000,
                Length = 2000,
                Diameter = 0,
                Weight = 150,
                PricePerUnit = 250000,
                StockQuantity = 45,
                UnitId = unit.Id,
                Unit = unit,
                Category = ProductCategoryEnum.Pipe,
                CurrencyId = currency.Id,
                Currency = currency
            };

            _contextMock.UnitsOfMeasure.Add(unit);
            _contextMock.Products.Add(product);
            await _contextMock.SaveChangesAsync();

            var command = new SimpleListCommand
            {
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _productSevicesMock.GetMailingProductsAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var mappedProduct = result.Data!.Items.FirstOrDefault(p => p.ProductId == product.Id);

            await Assert.That(mappedProduct).IsNotNull();
            await Assert.That(mappedProduct!.Name).IsEqualTo(product.Name);
            await Assert.That(mappedProduct.StockQuantity).IsEqualTo(45);
            await Assert.That(mappedProduct.StockPrice).IsEqualTo(250000);
            await Assert.That(mappedProduct.Dimmension).IsNotNull();
            await Assert.That(mappedProduct.Dimmension).IsNotEmpty();
        }

        [Test]
        public async Task GetMailingProductsAsync_WhenSearchTermProvided_FiltersResultsCorrectly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var unit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = "Sztuka",
                Symbol = "szt"
            };

            var steelGrade = CreateDummySteelGrade();
            var currency = CreateDummyCurrency();

            var targetProduct = new Product
            {
                Id = Guid.NewGuid(),
                Name = $"Ceownik Specjalny_{uniqueSuffix}",
                SteelGrade = steelGrade,
                SteelGradeId = steelGrade.Id,
                Thickness = 5,
                Width = 100,
                Length = 3000,
                Weight = 50,
                PricePerUnit = 120000,
                StockQuantity = 20,
                UnitId = unit.Id,
                Unit = unit,
                Category = ProductCategoryEnum.Profile,
                CurrencyId = currency.Id,
                Currency = currency
            };

            var otherProduct = new Product
            {
                Id = Guid.NewGuid(),
                Name = $"Kątownik zwykły_{uniqueSuffix}",
                SteelGrade = steelGrade,
                SteelGradeId = steelGrade.Id,
                Thickness = 4,
                Width = 50,
                Length = 3000,
                Weight = 30,
                PricePerUnit = 90000,
                StockQuantity = 15,
                UnitId = unit.Id,
                Unit = unit,
                Category = ProductCategoryEnum.Profile,
                CurrencyId = currency.Id,
                Currency = currency
            };

            _contextMock.UnitsOfMeasure.Add(unit);
            _contextMock.Products.AddRange(targetProduct, otherProduct);
            await _contextMock.SaveChangesAsync();

            var command = new SimpleListCommand
            {
                PageNumber = 1,
                PageSize = 10,
                SearchTerm = "Specjalny"
            };

            // Act
            var result = await _productSevicesMock.GetMailingProductsAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            var items = result.Data!.Items;

            await Assert.That(items).Count().IsEqualTo(1);
            await Assert.That(items.First().ProductId).IsEqualTo(targetProduct.Id);
            await Assert.That(items.First().Name).Contains("Specjalny");
        }

        // ─── AddProductAsync ───────────────────────────────────────────
        [Test]
        public async Task AddProductAsync_SuccessfullyAddsProduct_WhenDataIsValid()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var unit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = $"Sztuka_{uniqueSuffix}",
                Symbol = "szt."
            };

            var steelGrade = CreateDummySteelGrade("S350");

            _contextMock.UnitsOfMeasure.Add(unit);
            await _contextMock.SaveChangesAsync();

            var currencty = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Złoty",
                Code = "PLN",
                DecimalPlaces = 2,
            };
            _contextMock.Currencies.Add(currencty);
            await _contextMock.SaveChangesAsync();

            var command = new AddProductCommand
            {
                Name = $"Product_{uniqueSuffix}",
                SteelGradeId = steelGrade.Id,
                Thickness = 10,
                Width = 100,
                Length = 1000,
                Diameter = null,
                Weight = 5000,
                UnitId = unit.Id,
                PricePerUnit = 150000,
                StockQuantity = 50,
                Category = ProductCategoryEnum.Bar.ToString(),
                CurrencyId = currencty.Id
            };

            // Act
            var result = await _productSevicesMock.AddProductAsync(command);
            Console.WriteLine(result.Message);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status201Created);

            var dbProduct = await _contextMock.Products
                .Include(p => p.SteelGrade)
                .FirstOrDefaultAsync(p => p.Name == command.Name);

            await Assert.That(dbProduct).IsNotNull();

            await Assert.That(dbProduct!.SteelGrade.Name).IsEqualTo("S350");
            await Assert.That(dbProduct.SteelGradeId).IsEqualTo(steelGrade.Id);
            await Assert.That(dbProduct.PricePerUnit).IsEqualTo(150000);
        }

        [Test]
        public async Task AddProductAsync_Fails_WhenProductNameAlreadyExists()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            string productName = $"ExistingProduct_{uniqueSuffix}";

            var unit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = $"Sztuka_{uniqueSuffix}",
                Symbol = "szt."
            };

            var steelGrade = new SteelGrade
            {
                Id = Guid.NewGuid(),
                Name = "S235"
            };

            var currency = CreateDummyCurrency();

            var existingProduct = new Product
            {
                Name = productName,
                SteelGrade = steelGrade,
                SteelGradeId = steelGrade.Id,
                Thickness = 5,
                Width = 50,
                Length = 500,
                Weight = 1000,
                UnitId = unit.Id,
                PricePerUnit = 100000,
                StockQuantity = 10,
                Category = ProductCategoryEnum.Bar,
                Currency = currency,
                CurrencyId = currency.Id
            };

            _contextMock.UnitsOfMeasure.Add(unit);
            _contextMock.Products.Add(existingProduct);
            await _contextMock.SaveChangesAsync();

            var currencty = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Złoty",
                Code = "PLN",
                DecimalPlaces = 2,
            };
            _contextMock.Currencies.Add(currencty);
            await _contextMock.SaveChangesAsync();


            var command = new AddProductCommand
            {
                Name = productName,
                SteelGradeId = Guid.NewGuid(),
                Thickness = 10,
                Width = 100,
                Length = 1000,
                Weight = 5000,
                UnitId = unit.Id,
                PricePerUnit = 150000,
                StockQuantity = 50,
                Category = ProductCategoryEnum.Other.ToString(),
                CurrencyId = currencty.Id
            };

            // Act
            var result = await _productSevicesMock.AddProductAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.ProductAlreadyExists);
        }

        [Test]
        public async Task AddProductAsync_Fails_WhenUnitNotFound()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");


            var command = new AddProductCommand
            {
                Name = $"Product_{uniqueSuffix}",
                SteelGradeId = Guid.NewGuid(),
                Thickness = 10,
                Width = 100,
                Length = 1000,
                Weight = 5000,
                UnitId = Guid.NewGuid(),
                PricePerUnit = 150000,
                StockQuantity = 50,
                Category = "Plate",
                CurrencyId = Guid.NewGuid()
            };

            // Act
            var result = await _productSevicesMock.AddProductAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.NotFound);
        }

        [Test]
        public async Task AddProductAsync_Fails_WhenCategoryIsInvalid()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var unit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = $"Sztuka_{uniqueSuffix}",
                Symbol = "szt."
            };

            _contextMock.UnitsOfMeasure.Add(unit);
            await _contextMock.SaveChangesAsync();

            var command = new AddProductCommand
            {
                Name = $"Product_{uniqueSuffix}",
                SteelGradeId = Guid.NewGuid(),
                Thickness = 10,
                Width = 100,
                Length = 1000,
                Weight = 5000,
                UnitId = unit.Id,
                PricePerUnit = 150000,
                StockQuantity = 50,
                Category = "InvalidCategoryName",
                CurrencyId = Guid.NewGuid()
            };

            // Act
            var result = await _productSevicesMock.AddProductAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidCategory);
        }

        // ─── EditProductAsync ───────────────────────────────────────────

        [Test]
        public async Task EditProductAsync_SuccessfullyUpdatesAllFields_WhenDataIsValid()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var initialUnit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = $"Sztuka_{uniqueSuffix}",
                Symbol = "szt."
            };

            var newUnit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = $"Tona_{uniqueSuffix}",
                Symbol = "t"
            };

            var steelGrade = CreateDummySteelGrade("S235");
            var currency = CreateDummyCurrency();

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = $"StaraNazwa_{uniqueSuffix}",
                SteelGradeId = steelGrade.Id,
                SteelGrade = steelGrade,
                Thickness = 10,
                Width = 100,
                Length = 1000,
                Diameter = null,
                Weight = 1000,
                PricePerUnit = 10000,
                StockQuantity = 5,
                UnitId = initialUnit.Id,
                Unit = initialUnit,
                Category = ProductCategoryEnum.Bar,
                CurrencyId = currency.Id,
                Currency = currency
            };

            _contextMock.UnitsOfMeasure.AddRange(initialUnit, newUnit);
            _contextMock.Products.Add(product);
            await _contextMock.SaveChangesAsync();

            var command = new EditProductCommand
            {
                ProductId = product.Id,
                Name = $"NowaNazwa_{uniqueSuffix}",
                SteelGradeId = steelGrade.Id,
                Thickness = 20,
                Width = 200,
                Length = 2000,
                Diameter = 50,
                Weight = 2500,
                UnitId = newUnit.Id,
                PricePerUnit = 20000,
                StockQuantity = 15,
                Category = ProductCategoryEnum.Pipe.ToString()
            };

            // Act
            var result = await _productSevicesMock.EditProductAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("Product updated successfully.");

            var updatedProduct = await _contextMock.Products.FirstOrDefaultAsync(p => p.Id == product.Id);
            await Assert.That(updatedProduct).IsNotNull();
            await Assert.That(updatedProduct!.Name).IsEqualTo(command.Name);
            await Assert.That(updatedProduct.SteelGradeId).IsEqualTo(steelGrade.Id);
            await Assert.That(updatedProduct.Thickness).IsEqualTo(20);
            await Assert.That(updatedProduct.Width).IsEqualTo(200);
            await Assert.That(updatedProduct.Length).IsEqualTo(2000);
            await Assert.That(updatedProduct.Diameter).IsEqualTo(50);
            await Assert.That(updatedProduct.Weight).IsEqualTo(2500);
            await Assert.That(updatedProduct.UnitId).IsEqualTo(newUnit.Id);
            await Assert.That(updatedProduct.PricePerUnit).IsEqualTo(20000);
            await Assert.That(updatedProduct.StockQuantity).IsEqualTo(15);
            await Assert.That(updatedProduct.Category).IsEqualTo(ProductCategoryEnum.Pipe);
        }

        [Test]
        public async Task EditProductAsync_WhenPartialDataProvided_UpdatesOnlySpecifiedFields()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var unit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = $"Sztuka_{uniqueSuffix}",
                Symbol = "szt."
            };

            var steelGrade = CreateDummySteelGrade("S235");
            var currency = CreateDummyCurrency();

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = $"ProduktPrzedEdycja_{uniqueSuffix}",
                SteelGradeId = steelGrade.Id,
                SteelGrade = steelGrade,
                Thickness = 10,
                Width = 100,
                Length = 1000,
                Weight = 1000,
                PricePerUnit = 10000,
                StockQuantity = 50,
                UnitId = unit.Id,
                Unit = unit,
                Category = ProductCategoryEnum.Bar,
                CurrencyId = currency.Id,
                Currency = currency
            };

            _contextMock.UnitsOfMeasure.Add(unit);
            _contextMock.Products.Add(product);
            await _contextMock.SaveChangesAsync();

            var command = new EditProductCommand
            {
                ProductId = product.Id,
                Name = null,
                StockQuantity = 120
            };

            // Act
            var result = await _productSevicesMock.EditProductAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var updatedProduct = await _contextMock.Products.FirstOrDefaultAsync(p => p.Id == product.Id);
            await Assert.That(updatedProduct).IsNotNull();
            await Assert.That(updatedProduct!.Name).IsEqualTo($"ProduktPrzedEdycja_{uniqueSuffix}");
            await Assert.That(updatedProduct.StockQuantity).IsEqualTo(120);
            await Assert.That(updatedProduct.Thickness).IsEqualTo(10);
            await Assert.That(updatedProduct.PricePerUnit).IsEqualTo(10000);
        }

        [Test]
        public async Task EditProductAsync_Fails_WhenProductDoesNotExist()
        {
            // Arrange
            var command = new EditProductCommand
            {
                ProductId = Guid.NewGuid(),
                Name = "Nieistniejacy"
            };

            // Act
            var result = await _productSevicesMock.EditProductAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.ProductNotFound);
        }

        [Test]
        public async Task EditProductAsync_Fails_WhenNewNameAlreadyExistsInAnotherProduct()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            string existingName = $"ZajetaNazwa_{uniqueSuffix}";

            var unit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = $"Sztuka_{uniqueSuffix}",
                Symbol = "szt."
            };

            var steelGrade = CreateDummySteelGrade("S235");
            var currency = CreateDummyCurrency();

            var product1 = new Product
            {
                Id = Guid.NewGuid(),
                Name = existingName,
                SteelGrade = steelGrade,
                SteelGradeId = steelGrade.Id,
                Thickness = 1,
                Width = 1,
                Length = 1,
                UnitId = unit.Id,
                Unit = unit,
                Category = ProductCategoryEnum.Other,
                CurrencyId = currency.Id,
                Currency = currency
            };

            var product2 = new Product
            {
                Id = Guid.NewGuid(),
                Name = $"InnaNazwa_{uniqueSuffix}",
                SteelGrade = steelGrade,
                SteelGradeId = steelGrade.Id,
                Thickness = 1,
                Width = 1,
                Length = 1,
                UnitId = unit.Id,
                Unit = unit,
                Category = ProductCategoryEnum.Other,
                CurrencyId = currency.Id,
                Currency = currency
            };

            _contextMock.UnitsOfMeasure.Add(unit);
            _contextMock.Products.AddRange(product1, product2);
            await _contextMock.SaveChangesAsync();

            var command = new EditProductCommand
            {
                ProductId = product2.Id,
                Name = existingName
            };

            // Act
            var result = await _productSevicesMock.EditProductAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.ProductAlreadyExists);
        }

        [Test]
        public async Task EditProductAsync_Succeeds_WhenKeepingSameNameForSameProduct()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            string currentName = $"TaSamaNazwa_{uniqueSuffix}";

            var unit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = $"Sztuka_{uniqueSuffix}",
                Symbol = "szt."
            };

            var steelGrade = CreateDummySteelGrade("S355");
            var currency = CreateDummyCurrency();

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = currentName,
                SteelGrade = steelGrade,
                SteelGradeId = steelGrade.Id,
                Thickness = 1,
                Width = 1,
                Length = 1,
                UnitId = unit.Id,
                Unit = unit,
                Category = ProductCategoryEnum.Other,
                CurrencyId = currency.Id,
                Currency = currency
            };

            _contextMock.UnitsOfMeasure.Add(unit);
            _contextMock.Products.Add(product);
            await _contextMock.SaveChangesAsync();

            var command = new EditProductCommand
            {
                ProductId = product.Id,
                Name = $"   {currentName}   ",
                SteelGradeId = steelGrade.Id
            };

            // Act
            var result = await _productSevicesMock.EditProductAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
        }

        [Test]
        public async Task EditProductAsync_Fails_WhenUnitNotFound()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var unit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = $"Sztuka_{uniqueSuffix}",
                Symbol = "szt."
            };

            var steelGrade = CreateDummySteelGrade("S235");
            var currency = CreateDummyCurrency();

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = $"Produkt_{uniqueSuffix}",
                SteelGrade = steelGrade,
                SteelGradeId = steelGrade.Id,
                Thickness = 1,
                Width = 1,
                Length = 1,
                UnitId = unit.Id,
                Unit = unit,
                Category = ProductCategoryEnum.Other,
                CurrencyId = currency.Id,
                Currency = currency
            };

            _contextMock.UnitsOfMeasure.Add(unit);
            _contextMock.Products.Add(product);
            await _contextMock.SaveChangesAsync();

            var command = new EditProductCommand
            {
                ProductId = product.Id,
                UnitId = Guid.NewGuid()
            };

            // Act
            var result = await _productSevicesMock.EditProductAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.NotFound);
        }

        [Test]
        public async Task EditProductAsync_Fails_WhenCategoryIsInvalid()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var unit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = $"Sztuka_{uniqueSuffix}",
                Symbol = "szt."
            };

            var steelGrade = CreateDummySteelGrade("S235");
            var currency = CreateDummyCurrency();

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = $"Produkt_{uniqueSuffix}",
                SteelGradeId = steelGrade.Id,
                SteelGrade = steelGrade,
                Thickness = 1,
                Width = 1,
                Length = 1,
                UnitId = unit.Id,
                Unit = unit,
                Category = ProductCategoryEnum.Other,
                CurrencyId = currency.Id,
                Currency = currency
            };

            _contextMock.UnitsOfMeasure.Add(unit);
            _contextMock.Products.Add(product);
            await _contextMock.SaveChangesAsync();

            var command = new EditProductCommand
            {
                ProductId = product.Id,
                Category = "NiepoprawnaKategoria"
            };

            // Act
            var result = await _productSevicesMock.EditProductAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidCategory);
        }

        // ─── GetProductEditDetailAsync ──────────────────────────────────

        [Test]
        public async Task GetProductEditDetailAsync_WhenProductExists_ReturnsSuccessAndScalesValuesCorrectly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var unit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = $"Sztuka_{uniqueSuffix}",
                Symbol = "szt."
            };

            var steelGrade = CreateDummySteelGrade("S355");
            var currency = CreateDummyCurrency("Polski Złoty", "PLN");

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = $"ProduktDoEdycji_{uniqueSuffix}",
                SteelGradeId = steelGrade.Id,
                SteelGrade = steelGrade,
                UnitId = unit.Id,
                Unit = unit,
                CurrencyId = currency.Id,
                Currency = currency,
                Category = ProductCategoryEnum.Pipe,
                Thickness = 125,
                Width = 2000,
                Length = 60000,
                Diameter = 500,
                Weight = 15500,
                PricePerUnit = 255000,
                StockQuantity = 42
            };

            _contextMock.UnitsOfMeasure.Add(unit);
            _contextMock.Products.Add(product);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _productSevicesMock.GetProductEditDetailAsync(product.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();

            var data = result.Data!;
            await Assert.That(data.ProductId).IsEqualTo(product.Id);
            await Assert.That(data.Name).IsEqualTo(product.Name);
            await Assert.That(data.SteelGradeId).IsEqualTo(steelGrade.Id);
            await Assert.That(data.UnitId).IsEqualTo(unit.Id);
            await Assert.That(data.CurrencyId).IsEqualTo(currency.Id);
            await Assert.That(data.Category).IsEqualTo(ProductCategoryEnum.Pipe.ToString());

            await Assert.That(data.Thickness).IsEqualTo(12.5m);
            await Assert.That(data.Width).IsEqualTo(200.0m);
            await Assert.That(data.Length).IsEqualTo(6000.0m);
            await Assert.That(data.Diameter).IsEqualTo(50.0m);
            await Assert.That(data.Weight).IsEqualTo(15.5m);
            await Assert.That(data.PricePerUnit).IsEqualTo(25.5m);
            await Assert.That(data.StockQuantity).IsEqualTo(42);
        }

        [Test]
        public async Task GetProductEditDetailAsync_WhenProductHasNullDiameter_ReturnsNullDiameter()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var unit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = $"Metr_{uniqueSuffix}",
                Symbol = "m"
            };

            var steelGrade = CreateDummySteelGrade("S235");
            var currency = CreateDummyCurrency();

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = $"BlachaBezSrednicy_{uniqueSuffix}",
                SteelGradeId = steelGrade.Id,
                SteelGrade = steelGrade,
                UnitId = unit.Id,
                Unit = unit,
                CurrencyId = currency.Id,
                Currency = currency,
                Category = ProductCategoryEnum.Sheet,
                Thickness = 50,
                Width = 10000,
                Length = 20000,
                Diameter = null,
                Weight = 100000,
                PricePerUnit = 500000,
                StockQuantity = 10
            };

            _contextMock.UnitsOfMeasure.Add(unit);
            _contextMock.Products.Add(product);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _productSevicesMock.GetProductEditDetailAsync(product.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.Diameter).IsNull();
        }

        [Test]
        public async Task GetProductEditDetailAsync_WhenProductDoesNotExist_Returns404NotFound()
        {
            // Act
            var result = await _productSevicesMock.GetProductEditDetailAsync(Guid.NewGuid());

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.ProductNotFound);
            await Assert.That(result.Data).IsNull();
        }

        // ─── DeleteProductAsync ─────────────────────────────────────────────────

        [Test]
        public async Task DeleteProductAsync_WhenProductExists_AppliesSoftDeleteAndSetsCorrectFlags()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var productId = Guid.NewGuid();

            var unit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = $"Sztuka_{uniqueSuffix}",
                Symbol = "szt."
            };

            var steelGrade = CreateDummySteelGrade("S355");
            var currency = CreateDummyCurrency("Polski Złoty", "PLN");

            var product = new Product
            {
                Id = productId,
                Name = $"ProduktDoUsuniecia_{uniqueSuffix}",
                SteelGradeId = steelGrade.Id,
                SteelGrade = steelGrade,
                UnitId = unit.Id,
                Unit = unit,
                CurrencyId = currency.Id,
                Currency = currency,
                Category = ProductCategoryEnum.Pipe,
                Thickness = 125,
                Width = 2000,
                Length = 60000,
                Diameter = 500,
                Weight = 15500,
                PricePerUnit = 255000,
                StockQuantity = 42
            };

            _contextMock.UnitsOfMeasure.Add(unit);
            _contextMock.Products.Add(product);
            await _contextMock.SaveChangesAsync();

            var beforeDeleteTime = DateTime.UtcNow.AddSeconds(-1);

            // Act
            var result = await _productSevicesMock.DeleteProductAsync(productId);

            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("Product deleted successfully.");

            var standardQueriedProduct = await _contextMock.Products
                .FirstOrDefaultAsync(p => p.Id == productId);
            await Assert.That(standardQueriedProduct).IsNull();

            var rawDeletedProduct = await _contextMock.Products
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == productId);

            await Assert.That(rawDeletedProduct).IsNotNull();
            await Assert.That(rawDeletedProduct!.IsDeleted).IsTrue();
            await Assert.That(rawDeletedProduct.UpdateAt).IsNotNull();
            await Assert.That(rawDeletedProduct.UpdateAt!.Value).IsGreaterThanOrEqualTo(beforeDeleteTime);
        }

        [Test]
        public async Task DeleteProductAsync_WhenProductDoesntExists_Return404()
        {
            // Act
            var result = await _productSevicesMock.DeleteProductAsync(Guid.NewGuid());
            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.ProductNotFound);
            await Assert.That(result.Message).IsEqualTo("Product not found.");
        }

        // ─── SearchProductsAutocompleteAsync ─────────────────────────────────────────────────

        [Test]
        [Arguments(null)]
        [Arguments("")]
        [Arguments(" ")]
        [Arguments("a")]
        public async Task SearchProductsAutocompleteAsync_ReturnsEmptyList_WhenQueryIsLessThanTwoCharacters(string? query)
        {
            // Arrange
            var command = new SearchProductAutocompleteCommand
            {
                Query = query,
                Limit = 20
            };

            // Act
            var result = await _productSevicesMock.SearchProductsAutocompleteAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.Count).IsEqualTo(0);
        }

        [Test]
        public async Task SearchProductsAutocompleteAsync_FindsProducts_ByNameAndSteelGrade()
        {
            // Arrange
            var steelGrade304 = new SteelGrade { Id = Guid.NewGuid(), Name = "AISI 304" };
            var steelGrade316 = new SteelGrade { Id = Guid.NewGuid(), Name = "AISI 316L" };
            var currency = new Currency { Id = Guid.NewGuid(), Code = "PLN", DecimalPlaces = 2, Name = "Polski Złoty" };
            var unit = new UnitOfMeasure { Id = Guid.NewGuid(), Name = "Sztuka", Symbol = "szt." };

            _contextMock.SteelGrades.AddRange(steelGrade304, steelGrade316);
            _contextMock.Currencies.Add(currency);
            _contextMock.UnitsOfMeasure.Add(unit);

            var products = new List<Product>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "Profil Zamknięty 40x40",
                    SteelGrade = steelGrade304,
                    SteelGradeId = steelGrade304.Id,
                    CurrencyId = currency.Id,
                    UnitId = unit.Id,
                    PricePerUnit = 250000,
                    StockQuantity = 100,
                    Category = ProductCategoryEnum.Profile,
                    IsDeleted = false
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "Rura Nierdzewna 20mm",
                    SteelGrade = steelGrade304,
                    SteelGradeId = steelGrade304.Id,
                    CurrencyId = currency.Id,
                    UnitId = unit.Id,
                    PricePerUnit = 150000,
                    StockQuantity = 50,
                    Category = ProductCategoryEnum.Pipe,
                    IsDeleted = false
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "Blacha Gorącowalcowana",
                    SteelGrade = steelGrade316,
                    SteelGradeId = steelGrade316.Id,
                    CurrencyId = currency.Id,
                    UnitId = unit.Id,
                    PricePerUnit = 800000,
                    StockQuantity = 10,
                    Category = ProductCategoryEnum.Sheet,
                    IsDeleted = false
                }
            };

            _contextMock.Products.AddRange(products);
            await _contextMock.SaveChangesAsync();

            // Act
            var searchByNameCommand = new SearchProductAutocompleteCommand { Query = "Profil" };
            var resultByName = await _productSevicesMock.SearchProductsAutocompleteAsync(searchByNameCommand);

            var searchByGradeCommand = new SearchProductAutocompleteCommand { Query = "316L" };
            var resultByGrade = await _productSevicesMock.SearchProductsAutocompleteAsync(searchByGradeCommand);

            // Assert 
            await Assert.That(resultByName.IsSuccess).IsTrue();
            await Assert.That(resultByName.Data!.Count).IsEqualTo(1);
            await Assert.That(resultByName.Data![0].Name).IsEqualTo("Profil Zamknięty 40x40");
            await Assert.That(resultByName.Data![0].SteelGrade).IsEqualTo("AISI 304");
            await Assert.That(resultByGrade.IsSuccess).IsTrue();
            await Assert.That(resultByGrade.Data!.Count).IsEqualTo(1);
            await Assert.That(resultByGrade.Data![0].Name).IsEqualTo("Blacha Gorącowalcowana");
            await Assert.That(resultByGrade.Data![0].SteelGrade).IsEqualTo("AISI 316L");
        }

        [Test]
        public async Task SearchProductsAutocompleteAsync_IgnoresSoftDeletedProducts()
        {
            // Arrange
            var steelGrade = new SteelGrade { Id = Guid.NewGuid(), Name = "S235JR" };
            var currency = new Currency { Id = Guid.NewGuid(), Code = "PLN", DecimalPlaces = 2, Name = "Polski Złoty"};
            var unit = new UnitOfMeasure { Id = Guid.NewGuid(), Name = "Sztuka", Symbol = "szt." };

            _contextMock.SteelGrades.Add(steelGrade);
            _contextMock.Currencies.Add(currency);
            _contextMock.UnitsOfMeasure.Add(unit);

            var deletedProduct = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Kątownik Stalowy",
                SteelGrade = steelGrade,
                SteelGradeId = steelGrade.Id,
                CurrencyId = currency.Id,
                UnitId = unit.Id,
                PricePerUnit = 120000,
                StockQuantity = 20,
                Category = ProductCategoryEnum.Profile,
                IsDeleted = true
            };

            _contextMock.Products.Add(deletedProduct);
            await _contextMock.SaveChangesAsync();

            var command = new SearchProductAutocompleteCommand { Query = "Kątownik" };

            // Act
            var result = await _productSevicesMock.SearchProductsAutocompleteAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data!.Count).IsEqualTo(0);
        }

        [Test]
        public async Task SearchProductsAutocompleteAsync_EnforcesLimitBetweenOneAndFifty()
        {
            // Arrange
            var steelGrade = new SteelGrade { Id = Guid.NewGuid(), Name = "1.4301" };
            var currency = new Currency { Id = Guid.NewGuid(), Name = "Polski Złoty", Code = "PLN", DecimalPlaces = 2};
            var unit = new UnitOfMeasure { Id = Guid.NewGuid(), Name = "Sztuka", Symbol = "szt." };

            _contextMock.SteelGrades.Add(steelGrade);
            _contextMock.Currencies.Add(currency);
            _contextMock.UnitsOfMeasure.Add(unit);

            for (int i = 1; i <= 60; i++)
            {
                _contextMock.Products.Add(new Product
                {
                    Id = Guid.NewGuid(),
                    Name = $"Pręt Okrągły {i:D3}mm",
                    SteelGrade = steelGrade,
                    SteelGradeId = steelGrade.Id,
                    CurrencyId = currency.Id,
                    UnitId = unit.Id,
                    PricePerUnit = 100000 + i,
                    StockQuantity = 10,
                    Category = ProductCategoryEnum.Bar,
                    IsDeleted = false
                });
            }
            await _contextMock.SaveChangesAsync();

            var command = new SearchProductAutocompleteCommand
            {
                Query = "Pręt",
                Limit = 100
            };
            var result = await _productSevicesMock.SearchProductsAutocompleteAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data!.Count).IsEqualTo(50);
        }
    }
}
