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
using Services.Command.SteelGrade;
using Services.Services;
using Testcontainers.PostgreSql;

namespace Tests.Services
{
    public class SteelGradeServicesTest
    {
        protected AppDbContext _contextMock = null!;

        private static PostgreSqlContainer _dbContainer = null!;
        private static string _connectionString = null!;

        protected SteelGradeServices _steelGradeServicesMock = null!;
        protected ILogger<SteelGradeServices> _loggerMock = null!;
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

            _loggerMock = new LoggerFactory().CreateLogger<SteelGradeServices>();
            _steelGradeServicesMock = new SteelGradeServices(_contextMock, _loggerMock);
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
                Name = (name ?? $"S235_{Guid.NewGuid():N}").ToUpper(),
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

        private SteelGrade CreateDummySteelGradeWithDetails(string name, string standard, int density)
        {
            var steelGrade = new SteelGrade
            {
                Id = Guid.NewGuid(),
                Name = name.ToUpper(),
                Standard = standard,
                Density = density
            };
            _contextMock.SteelGrades.Add(steelGrade);
            return steelGrade;
        }

        private Product CreateDummyProduct(SteelGrade steelGrade, string name = "Produkt")
        {
            var unit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = $"Sztuka_{Guid.NewGuid():N}",
                Symbol = "szt."
            };
            var currency = CreateDummyCurrency();

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = name,
                SteelGradeId = steelGrade.Id,
                SteelGrade = steelGrade,
                UnitId = unit.Id,
                Unit = unit,
                CurrencyId = currency.Id,
                Currency = currency,
                Category = ProductCategoryEnum.Sheet,
                Thickness = 20,
                Width = 1000,
                Length = 2000,
                Diameter = null,
                Weight = 50000,
                PricePerUnit = 100000,
                StockQuantity = 10
            };

            _contextMock.UnitsOfMeasure.Add(unit);
            _contextMock.Products.Add(product);
            return product;
        }

        // ─── GetSteelGradesAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetSteelGradesAsync_WhenExist_ReturnsSuccessStatusAndNotNullData()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            CreateDummySteelGrade($"S355_{uniqueSuffix}");
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _steelGradeServicesMock.GetSteelGradesAsync();

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.Any(g => g.Name == $"S355_{uniqueSuffix}".ToUpper())).IsTrue();
        }

        [Test]
        public async Task GetSteelGradesAsync_WhenEmpty_ReturnsSuccessStatusAndEmptyData()
        {
            // Act
            var result = await _steelGradeServicesMock.GetSteelGradesAsync();

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data).IsEmpty();
        }

        // ─── GetSteelGradeListAsync ──────────────────────────────────────────────

        [Test]
        public async Task GetSteelGradeList_WhenStandardRequest_ReturnsPagedResult()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            CreateDummySteelGradeWithDetails($"1.4301_{uniqueSuffix}", "EN 10088", 7900);
            CreateDummySteelGradeWithDetails($"1.4404_{uniqueSuffix}", "EN 10088", 8000);
            await _contextMock.SaveChangesAsync();

            var command = new BasicListCommand
            {
                PageNumber = 1,
                PageSize = 10,
                SearchTerm = uniqueSuffix
            };

            // Act
            var result = await _steelGradeServicesMock.GetSteelGradeListAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.Items.Count()).IsEqualTo(2);
            await Assert.That(result.Data.TotalCount).IsEqualTo(2);
        }

        [Test]
        public async Task GetSteelGradeList_WhenSearchApplied_FiltersByNameStandardOrDensity()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            CreateDummySteelGradeWithDetails($"GradeA_{uniqueSuffix}", "DIN 17100", 7850);
            CreateDummySteelGradeWithDetails($"GradeB_{uniqueSuffix}", "SPECIAL_NORM", 7950);
            await _contextMock.SaveChangesAsync();

            var command = new BasicListCommand
            {
                PageNumber = 1,
                PageSize = 10,
                SearchTerm = "SPECIAL_NORM"
            };

            // Act
            var result = await _steelGradeServicesMock.GetSteelGradeListAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data!.Items.Count()).IsEqualTo(1);
            await Assert.That(result.Data.Items.First().Name).IsEqualTo($"GradeB_{uniqueSuffix}".ToUpper());
        }

        [Test]
        public async Task GetSteelGradeList_WhenSortingByDensityDescending_OrdersCorrectly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            CreateDummySteelGradeWithDetails($"LowDensity_{uniqueSuffix}", "STD", 7100);
            CreateDummySteelGradeWithDetails($"HighDensity_{uniqueSuffix}", "STD", 8500);
            await _contextMock.SaveChangesAsync();

            var command = new BasicListCommand
            {
                PageNumber = 1,
                PageSize = 10,
                SearchTerm = uniqueSuffix,
                SortBy = "density",
                SortDescending = true
            };

            // Act
            var result = await _steelGradeServicesMock.GetSteelGradeListAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();

            var items = result.Data!.Items.ToList();
            await Assert.That(items.First().Name).IsEqualTo($"HighDensity_{uniqueSuffix}".ToUpper());
            await Assert.That(items.Last().Name).IsEqualTo($"LowDensity_{uniqueSuffix}".ToUpper());
        }

        // ─── GetAssociatedProductsAsync ──────────────────────────────────────────

        [Test]
        public async Task GetAssociatedProductsAsync_WhenProductsExist_ReturnsListOfProducts()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var steelGrade = CreateDummySteelGrade($"Grade_{uniqueSuffix}");
            var product1 = CreateDummyProduct(steelGrade, $"Blacha_{uniqueSuffix}");
            var product2 = CreateDummyProduct(steelGrade, $"Rura_{uniqueSuffix}");
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _steelGradeServicesMock.GetAssociatedProductsAsync(steelGrade.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.Count).IsEqualTo(2);
            await Assert.That(result.Data.Any(p => p.Id == product1.Id)).IsTrue();
            await Assert.That(result.Data.Any(p => p.Id == product2.Id)).IsTrue();
        }

        [Test]
        public async Task GetAssociatedProductsAsync_WhenNoProductsExist_ReturnsEmptyList()
        {
            // Arrange
            var steelGrade = CreateDummySteelGrade();
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _steelGradeServicesMock.GetAssociatedProductsAsync(steelGrade.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.Count).IsEqualTo(0);
        }

        // ─── DeleteSteelGradeAsync ───────────────────────────────────────────────

        [Test]
        public async Task DeleteSteelGradeAsync_WhenGradeExistsWithoutProducts_DeletesSuccessfully()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var steelGrade = CreateDummySteelGrade($"S355_{uniqueSuffix}");
            await _contextMock.SaveChangesAsync();

            var beforeDeleteTime = DateTime.UtcNow.AddSeconds(-1);

            // Act
            var result = await _steelGradeServicesMock.DeleteSteelGradeAsync(steelGrade.Id, null);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var standardQueriedGrade = await _contextMock.SteelGrades
                .FirstOrDefaultAsync(s => s.Id == steelGrade.Id);
            await Assert.That(standardQueriedGrade).IsNull();

            var rawDeletedGrade = await _contextMock.SteelGrades
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.Id == steelGrade.Id);

            await Assert.That(rawDeletedGrade).IsNotNull();
            await Assert.That(rawDeletedGrade!.IsDeleted).IsTrue();
            await Assert.That(rawDeletedGrade.UpdateAt).IsNotNull();
            await Assert.That(rawDeletedGrade.UpdateAt!.Value).IsGreaterThanOrEqualTo(beforeDeleteTime);
        }

        [Test]
        public async Task DeleteSteelGradeAsync_WhenGradeHasProductsAndNoReassignments_ReturnsBadRequest()
        {
            // Arrange
            var steelGrade = CreateDummySteelGrade();
            CreateDummyProduct(steelGrade);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _steelGradeServicesMock.DeleteSteelGradeAsync(steelGrade.Id, null);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.SteelGradeInUse);
        }

        [Test]
        public async Task DeleteSteelGradeAsync_WhenGradeHasProductsAndReassignmentsGiven_ReassignsProductsAndDeletesGrade()
        {
            // Arrange
            var sourceGrade = CreateDummySteelGrade("SourceGrade");
            var targetGrade1 = CreateDummySteelGrade("TargetGrade1");
            var targetGrade2 = CreateDummySteelGrade("TargetGrade2");

            var product1 = CreateDummyProduct(sourceGrade, "Prod1");
            var product2 = CreateDummyProduct(sourceGrade, "Prod2");
            await _contextMock.SaveChangesAsync();

            var reassignments = new List<ProductReassignmentCommand>
            {
                new()
                {
                    ProductId =
                    product1.Id,
                    NewSteelGradeId =
                    targetGrade1.Id
                },
                new()
                {
                    ProductId = product2.Id,
                    NewSteelGradeId = targetGrade2.Id
                }
            };

            // Act
            var result = await _steelGradeServicesMock.DeleteSteelGradeAsync(sourceGrade.Id, reassignments);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var updatedProduct1 = await _contextMock.Products.FindAsync(product1.Id);
            var updatedProduct2 = await _contextMock.Products.FindAsync(product2.Id);

            await Assert.That(updatedProduct1!.SteelGradeId).IsEqualTo(targetGrade1.Id);
            await Assert.That(updatedProduct2!.SteelGradeId).IsEqualTo(targetGrade2.Id);

            var rawSourceGrade = await _contextMock.SteelGrades.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == sourceGrade.Id);
            await Assert.That(rawSourceGrade!.IsDeleted).IsTrue();
        }

        [Test]
        public async Task DeleteSteelGradeAsync_WhenTargetGradeIsTheSameAsDeletedGrade_ReturnsBadRequest()
        {
            // Arrange
            var steelGrade = CreateDummySteelGrade();
            var product = CreateDummyProduct(steelGrade);
            await _contextMock.SaveChangesAsync();

            var reassignments = new List<ProductReassignmentCommand>
            {
                new() { ProductId = product.Id, NewSteelGradeId = steelGrade.Id }
            };

            // Act
            var result = await _steelGradeServicesMock.DeleteSteelGradeAsync(steelGrade.Id, reassignments);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.BadRequest);
        }

        [Test]
        public async Task DeleteSteelGradeAsync_WhenTargetGradeDoesNotExist_ReturnsBadRequest()
        {
            // Arrange
            var steelGrade = CreateDummySteelGrade();
            var product = CreateDummyProduct(steelGrade);
            await _contextMock.SaveChangesAsync();

            var nonExistingTargetId = Guid.NewGuid();
            var reassignments = new List<ProductReassignmentCommand>
            {
                new() { ProductId = product.Id, NewSteelGradeId = nonExistingTargetId }
            };

            // Act
            var result = await _steelGradeServicesMock.DeleteSteelGradeAsync(steelGrade.Id, reassignments);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.BadRequest);
        }

        [Test]
        public async Task DeleteSteelGradeAsync_WhenGradeDoesNotExist_Returns404NotFound()
        {
            // Arrange 
            var nonExistingId = Guid.NewGuid();

            // Act
            var result = await _steelGradeServicesMock.DeleteSteelGradeAsync(nonExistingId, null);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.NotFound);
        }

        // ─── EditSteelGradeAsync ─────────────────────────────────────────────────

        [Test]
        public async Task EditSteelGradeAsync_WhenAllFieldsProvided_UpdatesSuccessfully()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var steelGrade = CreateDummySteelGradeWithDetails($"OldName_{uniqueSuffix}", "OldStandard", 7850);
            await _contextMock.SaveChangesAsync();

            var command = new EditSteelGradeCommand
            {
                Id = steelGrade.Id,
                Name = $"NewName_{uniqueSuffix}",
                Standard = "NewStandard",
                Density = 8000
            };

            // Act
            var result = await _steelGradeServicesMock.EditSteelGradeAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("Steel grade updated successfully");

            var updatedInDb = await _contextMock.SteelGrades.FindAsync(steelGrade.Id);
            await Assert.That(updatedInDb).IsNotNull();
            await Assert.That(updatedInDb!.Name).IsEqualTo($"NEWNAME_{uniqueSuffix.ToUpper()}");
            await Assert.That(updatedInDb.Standard).IsEqualTo("NewStandard");
            await Assert.That(updatedInDb.Density).IsEqualTo(8000);
        }

        [Test]
        public async Task EditSteelGradeAsync_WhenStandardIsEmptyOrWhitespace_ClearsStandardToNull()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var steelGrade = CreateDummySteelGradeWithDetails($"Grade_{uniqueSuffix}", "StandardToClear", 7850);
            await _contextMock.SaveChangesAsync();

            var command = new EditSteelGradeCommand
            {
                Id = steelGrade.Id,
                Standard = "   "
            };

            // Act
            var result = await _steelGradeServicesMock.EditSteelGradeAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();

            var updatedInDb = await _contextMock.SteelGrades.FindAsync(steelGrade.Id);
            await Assert.That(updatedInDb!.Standard).IsNull();
        }

        [Test]
        public async Task EditSteelGradeAsync_WhenSameNameProvided_DoesNotTriggerDuplicateError()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var steelGrade = CreateDummySteelGradeWithDetails($"UniqueName_{uniqueSuffix}", "Standard", 7850);
            await _contextMock.SaveChangesAsync();

            var command = new EditSteelGradeCommand
            {
                Id = steelGrade.Id,
                Name = $"UniqueName_{uniqueSuffix}",
                Density = 7900
            };

            // Act
            var result = await _steelGradeServicesMock.EditSteelGradeAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var updatedInDb = await _contextMock.SteelGrades.FindAsync(steelGrade.Id);
            await Assert.That(updatedInDb!.Density).IsEqualTo(7900);
        }

        [Test]
        public async Task EditSteelGradeAsync_WhenNameCollidesWithAnotherGrade_ReturnsBadRequest()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var grade1 = CreateDummySteelGradeWithDetails($"GradeOne_{uniqueSuffix}", "STD", 7850);
            var grade2 = CreateDummySteelGradeWithDetails($"GradeTwo_{uniqueSuffix}", "STD", 7850);
            await _contextMock.SaveChangesAsync();

            var command = new EditSteelGradeCommand
            {
                Id = grade2.Id,
                Name = $"GradeOne_{uniqueSuffix}"
            };

            // Act
            var result = await _steelGradeServicesMock.EditSteelGradeAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.SteelGradeAlreadyExist);
        }

        [Test]
        public async Task EditSteelGradeAsync_WhenGradeDoesNotExist_Returns404NotFound()
        {
            // Arrange
            var command = new EditSteelGradeCommand
            {
                Id = Guid.NewGuid(),
                Name = "NonExistent"
            };

            // Act
            var result = await _steelGradeServicesMock.EditSteelGradeAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.NotFound);
        }

        // ─── AddSteelGradeAsync ─────────────────────────────────────────────────

        [Test]
        public async Task AddSteelGradeAsync_WhenValidDataProvided_CreatesSteelGradeAndReturns201Created()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N").Substring(0, 6);
            var command = new AddSteelGradeCommand
            {
                Name = $"  s355jr_{uniqueSuffix}  ",
                Standard = "EN 10025-2",
                Density = 7850
            };

            // Act
            var result = await _steelGradeServicesMock.AddSteelGradeAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();

            var createdGrade = await _contextMock.SteelGrades
                .FirstOrDefaultAsync(s => s.Name == $"S355JR_{uniqueSuffix.ToUpper()}");

            await Assert.That(createdGrade).IsNotNull();
            await Assert.That(createdGrade!.Name).IsEqualTo($"S355JR_{uniqueSuffix.ToUpper()}");
        }

        [Test]
        public async Task AddSteelGradeAsync_WhenStandardIsEmptyOrWhitespace_SavesStandardAsNull()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var command = new AddSteelGradeCommand
            {
                Name = $"1.4301_{uniqueSuffix}",
                Standard = "   ",
                Density = 7900
            };

            // Act
            var result = await _steelGradeServicesMock.AddSteelGradeAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status201Created);

            var createdGrade = await _contextMock.SteelGrades
                .FirstOrDefaultAsync(s => s.Name == $"1.4301_{uniqueSuffix.ToUpper()}");

            await Assert.That(createdGrade).IsNotNull();
            await Assert.That(createdGrade!.Standard).IsNull();
        }

        [Test]
        public async Task AddSteelGradeAsync_WhenSteelGradeNameAlreadyExists_ReturnsBadRequestAndConflictErrorCode()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            CreateDummySteelGrade($"S235JR_{uniqueSuffix}");
            await _contextMock.SaveChangesAsync();

            var command = new AddSteelGradeCommand
            {
                Name = $"s235jr_{uniqueSuffix}",
                Standard = "EN 10025",
                Density = 7850
            };

            // Act
            var result = await _steelGradeServicesMock.AddSteelGradeAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.SteelGradeAlreadyExist);
        }
    }
}
