using Domain.Models;
using DTO.Request;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
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

            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(_connectionString, options =>
                {
                    options.UseNetTopologySuite();
                })
                .Options;
            using var context = new AppDbContext(dbOptions);
            await context.Database.EnsureCreatedAsync();

            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
        }

        [After(Class)]
        public static async Task CleanupClassAsync()
            => await _dbContainer.DisposeAsync();

        [Before(Test)]
        public async Task SetupAsync()
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(_connectionString, options =>
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
            if (_contextMock != null)
            {
                await _contextMock.DisposeAsync();
            }
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

            var category = new ProductCategory
            {
                Id = Guid.NewGuid(),
                Name = $"Stal_{uniqueSuffix}",
                Description = "Kategoria dla produktów stalowych"
            };

            var type = new ProductType
            {
                Id = Guid.NewGuid(),
                Name = "Rura",
                CategoryId = category.Id,
                Category = category,
                Description = "Typ dla produktów stalowych"
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
                ProductTypeId = type.Id,
                ProductType = type
            };

            _contextMock.UnitsOfMeasure.Add(unit);
            _contextMock.ProductCategories.Add(category);
            _contextMock.ProductTypes.Add(type);
            _contextMock.Products.Add(product);
            await _contextMock.SaveChangesAsync();

            var pagged = new PaggedRequest
            {
                PageNumber = 1,
                PageSize = 10
            };

            var sorting = new SortingRequest();
            var search = new SearchRequest();
            var filter = new ProductFilterRequest();

            // Act
            var result = await _productSevicesMock.GetProductListAsync(pagged, sorting, search, filter);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var mappedProduct = result.Data!.Items.FirstOrDefault(p => p.Id == product.Id);

            await Assert.That(mappedProduct).IsNotNull();
            await Assert.That(mappedProduct!.Name).IsEqualTo(product.Name);
            await Assert.That(mappedProduct.SteelGrade).IsEqualTo("S235");
            await Assert.That(mappedProduct.StockQuantity).IsEqualTo(100);
            await Assert.That(mappedProduct.Category).IsEqualTo(category.Name);
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

            var category = new ProductCategory
            {
                Id = Guid.NewGuid(),
                Name = $"Kat_{uniqueSuffix}",
                Description = "Kategoria dla produktów stalowych"
            };

            var type = new ProductType
            {
                Id = Guid.NewGuid(),
                Name = "Profil",
                CategoryId = category.Id,
                Category = category,
                Description = "Typ dla produktów stalowych"
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
                ProductTypeId = type.Id,
                ProductType = type
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
                ProductTypeId = type.Id,
                ProductType = type
            };

            _contextMock.UnitsOfMeasure.Add(unit);
            _contextMock.ProductCategories.Add(category);
            _contextMock.ProductTypes.Add(type);
            _contextMock.Products.AddRange(targetProduct, otherProduct);
            await _contextMock.SaveChangesAsync();

            var pagged = new PaggedRequest { PageNumber = 1, PageSize = 10 };
            var sorting = new SortingRequest();

            var search = new SearchRequest { SearchTerm = "zamknięty" };
            var filter = new ProductFilterRequest();

            // Act
            var result = await _productSevicesMock.GetProductListAsync(pagged, sorting, search, filter);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            var items = result.Data!.Items;
            await Assert.That(items.Any(p => p.Id == targetProduct.Id)).IsTrue();
            await Assert.That(items.Any(p => p.Id == otherProduct.Id)).IsFalse();
        }

        [Test]
        public async Task GetProductListAsync_WhenNoProductsMatch_ReturnsEmptyListWithSuccessStatus() // Zmieniona nazwa
        {
            // Arrange
            var pagged = new PaggedRequest { PageNumber = 1, PageSize = 10 };
            var sorting = new SortingRequest();

            var search = new SearchRequest { SearchTerm = Guid.NewGuid().ToString() };
            var filter = new ProductFilterRequest();

            // Act
            var result = await _productSevicesMock.GetProductListAsync(pagged, sorting, search, filter);

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

            var catSteel = new ProductCategory
            {
                Id = Guid.NewGuid(),
                Name = $"Stal_{uniqueSuffix}",
                Description = "Kategoria dla produktów stalowych"
            };

            var catAlu = new ProductCategory
            {
                Id = Guid.NewGuid(),
                Name = $"Aluminium_{uniqueSuffix}",
                Description = "Kategoria dla produktów aluminiowych"
            };

            var typeSteel = new ProductType
            {
                Id = Guid.NewGuid(),
                Name = "Rura",
                CategoryId = catSteel.Id,
                Category = catSteel,
                Description = "Typ dla produktów stalowych"
            };

            var typeAlu = new ProductType
            {
                Id = Guid.NewGuid(),
                Name = "Profil",
                CategoryId = catAlu.Id,
                Category = catAlu,
                Description = "Typ dla produktów aluminiowych"
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
                ProductTypeId = typeSteel.Id,
                ProductType = typeSteel
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
                ProductTypeId =
                typeSteel.Id,
                ProductType = typeSteel
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
                ProductTypeId = typeAlu.Id,
                ProductType = typeAlu
            };

            _contextMock.UnitsOfMeasure.Add(unit);
            _contextMock.ProductCategories.AddRange(catSteel, catAlu);
            _contextMock.ProductTypes.AddRange(typeSteel, typeAlu);
            _contextMock.Products.AddRange(p1, p2, p3);
            await _contextMock.SaveChangesAsync();

            var pagged = new PaggedRequest
            {
                PageNumber = 1,
                PageSize = 10
            };

            var search = new SearchRequest();

            var filter = new ProductFilterRequest
            {
                ProductCategory = catSteel.Name
            };

            var sorting = new SortingRequest { SortBy = "quantity", SortDescending = true };

            // Act
            var result = await _productSevicesMock.GetProductListAsync(
                pagged,
                sorting,
                search,
                filter
                );

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            var items = result.Data!.Items;

            await Assert.That(items).HasCount().EqualTo(2);
            await Assert.That(items[0].Id).IsEqualTo(p1.Id);
            await Assert.That(items[1].Id).IsEqualTo(p2.Id);
        }

        // ─── GetProductCategoryAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetProductCategoryAsync_ReturnsDistinctAndSortedCategories()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var cat1 = new ProductCategory
            {
                Id = Guid.NewGuid(),
                Name = $"Zebra_{uniqueSuffix}",
                Description = "Zebra"
            };

            var cat2 = new ProductCategory
            {
                Id = Guid.NewGuid(),
                Name = $"Alpaka_{uniqueSuffix}",
                Description = "Alpaka"
            };

            var cat3 = new ProductCategory
            {
                Id = Guid.NewGuid(),
                Name = $"Zebra_{uniqueSuffix}",
                Description = "Zebra"
            };

            var cat4 = new ProductCategory
            {
                Id = Guid.NewGuid(),
                Name = $"Delfin_{uniqueSuffix}",
                Description = "Delfin"
            };

            _contextMock.ProductCategories.AddRange(cat1, cat2, cat3, cat4);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _productSevicesMock.GetProductCategoryAsync();

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var myCategories = result.Data!.Where(c => c.EndsWith(uniqueSuffix)).ToList();

            await Assert.That(myCategories).HasCount().EqualTo(3);
            await Assert.That(myCategories[0]).IsEqualTo($"Alpaka_{uniqueSuffix}");
            await Assert.That(myCategories[1]).IsEqualTo($"Delfin_{uniqueSuffix}");
            await Assert.That(myCategories[2]).IsEqualTo($"Zebra_{uniqueSuffix}");
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

            var category = new ProductCategory
            {
                Id = Guid.NewGuid(),
                Name = $"Cat_{uniqueSuffix}",
                Description = "Kategoria dla produktów stalowych"
            };

            var type = new ProductType
            {
                Id = Guid.NewGuid(),
                Name = "Typ",
                CategoryId = category.Id,
                Category = category,
                Description = $"{uniqueSuffix}"
            };


            var p1 = new Product
            {
                Id = Guid.NewGuid(),
                Name = "P1",
                SteelGrade = $"S355_{uniqueSuffix}",
                UnitId = unit.Id,
                Unit = unit,
                ProductTypeId = type.Id,
                ProductType = type,
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
                ProductTypeId = type.Id,
                ProductType = type,
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
                ProductTypeId = type.Id,
                ProductType = type,
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
                ProductTypeId = type.Id,
                ProductType = type,
                Thickness = 1,
                Width = 1,
                Length = 1
            };

            _contextMock.UnitsOfMeasure.Add(unit);
            _contextMock.ProductCategories.Add(category);
            _contextMock.ProductTypes.Add(type);
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
