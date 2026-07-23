using Domain.Enum;
using Domain.Models;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Services.Command;
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
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $"CREATE SCHEMA {_currentSchema};";
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            var testConnectionString = $"{_connectionString};SearchPath={_currentSchema},public";
            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(testConnectionString, options =>
                {
                    options.UseNetTopologySuite();
                })
                .Options;

            _contextMock = new AppDbContext(dbOptions);

            await _contextMock.Database.EnsureCreatedAsync();

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

        // ─── GetProductListAsync ─────────────────────────────────────────────────

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


            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = $"Rura Czarna_{uniqueSuffix}",
                SteelGrade = "S235",
                Thickness = 2,
                Width = 0,
                Length = 6000,
                Diameter = 50,
                Weight = 15,
                PricePerUnit = 500000,
                StockQuantity = 100,
                UnitId = unit.Id,
                Unit = unit,
                Category = ProductCategoryEnum.Standard
            };

            _contextMock.UnitsOfMeasure.Add(unit);
            _contextMock.Products.Add(product);
            await _contextMock.SaveChangesAsync();

            var command = new ProductListCommand
            {
                PageNumber = 1,
                PageSize = 10
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
            await Assert.That(mappedProduct.Category).IsEqualTo(ProductCategoryEnum.Standard.ToString());
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

            var targetProduct = new Product
            {
                Id = Guid.NewGuid(),
                Name = $"Profil Zamknięty_{uniqueSuffix}",
                SteelGrade = "S355",
                Thickness = 3,
                Width = 40,
                Length = 6000,
                Weight = 20,
                PricePerUnit = 100,
                StockQuantity = 50,
                UnitId = unit.Id,
                Unit = unit,
                Category = ProductCategoryEnum.Profile
            };

            var otherProduct = new Product
            {
                Id = Guid.NewGuid(),
                Name = $"Blacha_{uniqueSuffix}",
                SteelGrade = "S235",
                Thickness = 5,
                Width = 1000,
                Length = 2000,
                Weight = 100,
                PricePerUnit = 200,
                StockQuantity = 10,
                UnitId = unit.Id,
                Unit = unit,
                Category = ProductCategoryEnum.Standard
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

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("No products found.");
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.Items).IsEmpty();
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


            var p1 = new Product
            {
                Id = Guid.NewGuid(),
                Name = $"A_Rura_{uniqueSuffix}",
                SteelGrade = "S235",
                Thickness = 2,
                Width = 0,
                Length = 6000,
                StockQuantity = 50,
                UnitId = unit.Id,
                Unit = unit,
                Category = ProductCategoryEnum.Standard
            };

            var p2 = new Product
            {
                Id = Guid.NewGuid(),
                Name = $"B_Rura_{uniqueSuffix}",
                SteelGrade = "S355",
                Thickness = 5,
                Width = 0,
                Length = 6000,
                StockQuantity = 10,
                UnitId = unit.Id,
                Unit = unit,
                Category= ProductCategoryEnum.Standard
            };

            var p3 = new Product
            {
                Id = Guid.NewGuid(),
                Name = $"C_Profil_{uniqueSuffix}",
                SteelGrade = "AW6060",
                Thickness = 2,
                Width = 20,
                Length = 6000,
                StockQuantity = 100,
                UnitId = unit.Id,
                Unit = unit,
                Category = ProductCategoryEnum.Profile
            };

            _contextMock.UnitsOfMeasure.Add(unit);
            _contextMock.Products.AddRange(p1, p2, p3);
            await _contextMock.SaveChangesAsync();

            var command = new ProductListCommand
            {
                PageNumber = 1,
                PageSize = 10,
                ProductCategory = ProductCategoryEnum.Standard.ToString(),
                SortBy = "quantity",
                SortDescending = true
            };

            // Act
            var result = await _productSevicesMock.GetProductListAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            var items = result.Data!.Items;

            await Assert.That(items).HasCount().EqualTo(2);
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

            await Assert.That(returnedCategories).HasCount().EqualTo(expectedCategories.Count);

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

        // ─── GetSteelGradesAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetSteelGradesAsync_ReturnsDistinctAndSortedSteelGrades()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var unit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = "Sztuka",
                Symbol = "szt"
            };


            var p1 = new Product
            {
                Id = Guid.NewGuid(),
                Name = "P1",
                SteelGrade = $"S355_{uniqueSuffix}",
                UnitId = unit.Id,
                Unit = unit,
                Category = ProductCategoryEnum.Standard,
                Thickness = 1,
                Width = 1,
                Length = 1
            };

            var p2 = new Product
            {
                Id = Guid.NewGuid(),
                Name = "P2",
                SteelGrade = $"AW6060_{uniqueSuffix}",
                UnitId = unit.Id,
                Unit = unit,
                Category = ProductCategoryEnum.Standard,
                Thickness = 1,
                Width = 1,
                Length = 1
            };

            var p3 = new Product
            {
                Id = Guid.NewGuid(),
                Name = "P3",
                SteelGrade = $"S355_{uniqueSuffix}",
                UnitId = unit.Id,
                Unit = unit,
                Category = ProductCategoryEnum.Standard,
                Thickness = 1,
                Width = 1,
                Length = 1
            };

            var p4 = new Product
            {
                Id = Guid.NewGuid(),
                Name = "P4",
                SteelGrade = $"S235_{uniqueSuffix}",
                UnitId = unit.Id,
                Unit = unit,
                Category = ProductCategoryEnum.Standard,
                Thickness = 1,
                Width = 1,
                Length = 1
            };

            _contextMock.UnitsOfMeasure.Add(unit);
            _contextMock.Products.AddRange(p1, p2, p3, p4);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _productSevicesMock.GetSteelGradesAsync();

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var myGrades = result.Data!.Where(g => g.EndsWith(uniqueSuffix)).ToList();

            await Assert.That(myGrades).HasCount().EqualTo(3);
            await Assert.That(myGrades[0]).IsEqualTo($"AW6060_{uniqueSuffix}");
            await Assert.That(myGrades[1]).IsEqualTo($"S235_{uniqueSuffix}");
            await Assert.That(myGrades[2]).IsEqualTo($"S355_{uniqueSuffix}");
        }

        [Test]
        public async Task GetSteelGradesAsync_WhenCalled_ReturnsSuccessStatusAndNotNullData()
        {
            // Act
            var result = await _productSevicesMock.GetSteelGradesAsync();

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();
        }
    }
}
