using Domain.Constants;
using Domain.Models;
using Infrastructure;
using Infrastructure.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Services.Command.List;
using Services.Command.Unit;
using Services.Services;
using Testcontainers.PostgreSql;

namespace Tests.Services
{
    public class UnitServicesTest
    {
        protected AppDbContext _contextMock = null!;

        private static PostgreSqlContainer _dbContainer = null!;
        private static string _connectionString = null!;

        protected UnitServices _unitServicesMok = null!;
        protected ILogger<UnitServices> _loggerMock = null!;

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

            _loggerMock = new LoggerFactory().CreateLogger<UnitServices>();

            _unitServicesMok = new UnitServices(_contextMock, _loggerMock);
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

        // ─── GetSimpleUnitList ─────────────────────────────────────────────────
        [Test]
        public async Task GetSimpleUnitList_ReturnsUnitsSuccessfully()
        {
            // Arrange
            var unit1 = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = "Kilogram",
                Symbol = "kg",
                BaseMultiplier = 1
            };

            var unit2 = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = "Sztuka",
                Symbol = "szt",
                BaseMultiplier = 1
            };

            _contextMock.UnitsOfMeasure.AddRange(unit1, unit2);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _unitServicesMok.GetSimpleUnitList();

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var items = result.Data!;
            await Assert.That(items).Count().IsEqualTo(2);

            await Assert.That(items.Any(u => u.Id == unit1.Id && u.Name == "Kilogram" && u.Symbol == "kg")).IsTrue();
            await Assert.That(items.Any(u => u.Id == unit2.Id && u.Name == "Sztuka" && u.Symbol == "szt")).IsTrue();
        }

        [Test]
        public async Task GetSimpleUnitList_ReturnsEmptyListWhenNoUnitsExist()
        {
            // Act
            var result = await _unitServicesMok.GetSimpleUnitList();

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!).IsEmpty();
        }

        // ─── GetUnitListAsync ──────────────────────────────────────────────────
        [Test]
        public async Task GetUnitListAsync_ReturnsPagedUnitsSuccessfully()
        {
            // Arrange
            var unit1 = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = "Kilogram",
                Symbol = "kg",
                BaseMultiplier = 1
            };

            var unit2 = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = "Gram",
                Symbol = "g",
                BaseMultiplier = 1000
            };

            _contextMock.UnitsOfMeasure.AddRange(unit1, unit2);
            await _contextMock.SaveChangesAsync();

            var command = new BasicListCommand
            {
                PageNumber = 1,
                PageSize = 10,
                SearchTerm = string.Empty,
                SortBy = "name",
                SortDescending = false
            };

            // Act
            var result = await _unitServicesMok.GetUnitListAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var pagedResult = result.Data!;
            await Assert.That(pagedResult.TotalCount).IsEqualTo(2);
            await Assert.That(pagedResult.Items).Count().IsEqualTo(2);

            var firstItem = pagedResult.Items.First();
            await Assert.That(firstItem.Name).IsEqualTo("Gram");
            await Assert.That(firstItem.Symbol).IsEqualTo("g");
            await Assert.That(firstItem.BaseMultiplier).IsEqualTo(1000);
        }

        [Test]
        public async Task GetUnitListAsync_AppliesSearchFilterCorrectly()
        {
            // Arrange
            var unit1 = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = "Metr",
                Symbol = "m",
                BaseMultiplier = 1
            };

            var unit2 = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = "Kilogram",
                Symbol = "kg",
                BaseMultiplier = 1
            };

            _contextMock.UnitsOfMeasure.AddRange(unit1, unit2);
            await _contextMock.SaveChangesAsync();

            var command = new BasicListCommand
            {
                PageNumber = 1,
                PageSize = 10,
                SearchTerm = "Metr",
                SortBy = "name",
                SortDescending = false
            };

            // Act
            var result = await _unitServicesMok.GetUnitListAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var pagedResult = result.Data!;
            await Assert.That(pagedResult.TotalCount).IsEqualTo(1);
            await Assert.That(pagedResult.Items.First().Name).IsEqualTo("Metr");
        }

        [Test]
        public async Task GetUnitListAsync_RespectsPagination()
        {
            // Arrange
            var units = Enumerable.Range(1, 15).Select(i => new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = $"Jednostka {i:D2}",
                Symbol = $"j{i}",
                BaseMultiplier = 1
            }).ToArray();

            _contextMock.UnitsOfMeasure.AddRange(units);
            await _contextMock.SaveChangesAsync();

            var command = new BasicListCommand
            {
                PageNumber = 2,
                PageSize = 5,
                SearchTerm = string.Empty,
                SortBy = "name",
                SortDescending = false
            };

            // Act
            var result = await _unitServicesMok.GetUnitListAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var pagedResult = result.Data!;
            await Assert.That(pagedResult.TotalCount).IsEqualTo(15);
            await Assert.That(pagedResult.TotalPages).IsEqualTo(3);
            await Assert.That(pagedResult.Items).Count().IsEqualTo(5);
        }

        // ─── AddUnitAsync ──────────────────────────────────────────────────────
        [Test]
        public async Task AddUnitAsync_AddsUnitSuccessfully_WhenValidDataProvided()
        {
            // Arrange
            var command = new AddUnitCommand
            {
                Name = "Metr bieżący",
                Symbol = "mb",
                BaseMultiplier = 1
            };

            // Act
            var result = await _unitServicesMok.AddUnitAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(Microsoft.AspNetCore.Http.StatusCodes.Status201Created);

            var savedUnit = await _contextMock.UnitsOfMeasure
                .FirstOrDefaultAsync(u => u.Symbol == "mb");

            await Assert.That(savedUnit).IsNotNull();
            await Assert.That(savedUnit!.Name).IsEqualTo("Metr bieżący");
            await Assert.That(savedUnit.BaseMultiplier).IsEqualTo(1);
        }

        [Test]
        public async Task AddUnitAsync_ReturnsConflict_WhenUnitWithSameNameAlreadyExists()
        {
            // Arrange
            var existingUnit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = "Kilogram",
                Symbol = "kg",
                BaseMultiplier = 1
            };

            _contextMock.UnitsOfMeasure.Add(existingUnit);
            await _contextMock.SaveChangesAsync();

            var command = new AddUnitCommand
            {
                Name = "Kilogram",
                Symbol = "kilo",
                BaseMultiplier = 1
            };

            // Act
            var result = await _unitServicesMok.AddUnitAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(Microsoft.AspNetCore.Http.StatusCodes.Status409Conflict);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.UnitAlreadyExists);

            var count = await _contextMock.UnitsOfMeasure.CountAsync();
            await Assert.That(count).IsEqualTo(1);
        }

        [Test]
        public async Task AddUnitAsync_ReturnsConflict_WhenUnitWithSameSymbolAlreadyExists()
        {
            // Arrange
            var existingUnit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = "Kilogram",
                Symbol = "kg",
                BaseMultiplier = 1
            };

            _contextMock.UnitsOfMeasure.Add(existingUnit);
            await _contextMock.SaveChangesAsync();

            var command = new AddUnitCommand
            {
                Name = "Nowy Kilogram",
                Symbol = "kg",
                BaseMultiplier = 1000
            };

            // Act
            var result = await _unitServicesMok.AddUnitAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(Microsoft.AspNetCore.Http.StatusCodes.Status409Conflict);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.UnitAlreadyExists);

            var count = await _contextMock.UnitsOfMeasure.CountAsync();
            await Assert.That(count).IsEqualTo(1);
        }

        // ─── EditUnitAsync ─────────────────────────────────────────────────────
        [Test]
        public async Task EditUnitAsync_UpdatesAllFieldsSuccessfully_WhenValidDataProvided()
        {
            // Arrange
            var unit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = "Metr",
                Symbol = "m",
                BaseMultiplier = 1
            };

            _contextMock.UnitsOfMeasure.Add(unit);
            await _contextMock.SaveChangesAsync();

            var command = new EditUnitCommand
            {
                UnitId = unit.Id,
                Name = "Metr Bieżący",
                Symbol = "mb",
                BaseMultiplier = 10
            };

            // Act
            var result = await _unitServicesMok.EditUnitAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(Microsoft.AspNetCore.Http.StatusCodes.Status200OK);

            var updated = await _contextMock.UnitsOfMeasure.FindAsync(unit.Id);
            await Assert.That(updated).IsNotNull();
            await Assert.That(updated!.Name).IsEqualTo("Metr Bieżący");
            await Assert.That(updated.Symbol).IsEqualTo("mb");
            await Assert.That(updated.BaseMultiplier).IsEqualTo(10);
        }

        [Test]
        public async Task EditUnitAsync_UpdatesOnlyProvidedFields_WhenPartialDataProvided()
        {
            // Arrange
            var unit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = "Kilogram",
                Symbol = "kg",
                BaseMultiplier = 1000
            };

            _contextMock.UnitsOfMeasure.Add(unit);
            await _contextMock.SaveChangesAsync();

            var command = new EditUnitCommand
            {
                UnitId = unit.Id,
                Name = "Kilogram Modyfikowany"
            };

            // Act
            var result = await _unitServicesMok.EditUnitAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(Microsoft.AspNetCore.Http.StatusCodes.Status200OK);

            var updated = await _contextMock.UnitsOfMeasure.FindAsync(unit.Id);
            await Assert.That(updated).IsNotNull();
            await Assert.That(updated!.Name).IsEqualTo("Kilogram Modyfikowany");
            await Assert.That(updated.Symbol).IsEqualTo("kg");
            await Assert.That(updated.BaseMultiplier).IsEqualTo(1000);
        }

        [Test]
        public async Task EditUnitAsync_ReturnsNotFound_WhenUnitDoesNotExist()
        {
            // Arrange
            var command = new EditUnitCommand
            {
                UnitId = Guid.NewGuid(),
                Name = "Nieistniejący"
            };

            // Act
            var result = await _unitServicesMok.EditUnitAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(Microsoft.AspNetCore.Http.StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.UnitNotFound);
        }

        [Test]
        public async Task EditUnitAsync_ReturnsConflict_WhenNewNameAlreadyExistsInAnotherUnit()
        {
            // Arrange
            var unitToEdit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = "Gram",
                Symbol = "g",
                BaseMultiplier = 1
            };

            var anotherUnit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = "Kilogram",
                Symbol = "kg",
                BaseMultiplier = 1000
            };

            _contextMock.UnitsOfMeasure.AddRange(unitToEdit, anotherUnit);
            await _contextMock.SaveChangesAsync();

            var command = new EditUnitCommand
            {
                UnitId = unitToEdit.Id,
                Name = "  kilogram  "
            };

            // Act
            var result = await _unitServicesMok.EditUnitAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(Microsoft.AspNetCore.Http.StatusCodes.Status409Conflict);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.UnitAlreadyExists);
        }

        [Test]
        public async Task EditUnitAsync_ReturnsConflict_WhenNewSymbolAlreadyExistsInAnotherUnit()
        {
            // Arrange
            var unitToEdit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = "Tona",
                Symbol = "t",
                BaseMultiplier = 1000000
            };

            var anotherUnit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = "Kilogram",
                Symbol = "kg",
                BaseMultiplier = 1000
            };

            _contextMock.UnitsOfMeasure.AddRange(unitToEdit, anotherUnit);
            await _contextMock.SaveChangesAsync();

            var command = new EditUnitCommand
            {
                UnitId = unitToEdit.Id,
                Symbol = "  KG  "
            };

            // Act
            var result = await _unitServicesMok.EditUnitAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(Microsoft.AspNetCore.Http.StatusCodes.Status409Conflict);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.UnitAlreadyExists);
        }

        [Test]
        public async Task EditUnitAsync_DoesNotConflictWithItself_WhenSameNameAndSymbolPassed()
        {
            // Arrange
            var unit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = "Sztuka",
                Symbol = "szt",
                BaseMultiplier = 1
            };

            _contextMock.UnitsOfMeasure.Add(unit);
            await _contextMock.SaveChangesAsync();

            var command = new EditUnitCommand
            {
                UnitId = unit.Id,
                Name = "Sztuka",
                Symbol = "szt",
                BaseMultiplier = 5
            };

            // Act
            var result = await _unitServicesMok.EditUnitAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(Microsoft.AspNetCore.Http.StatusCodes.Status200OK);

            var updated = await _contextMock.UnitsOfMeasure.FindAsync(unit.Id);
            await Assert.That(updated!.BaseMultiplier).IsEqualTo(5);
        }
    }
}
