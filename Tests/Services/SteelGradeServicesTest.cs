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
                Name = name ?? $"S235_{Guid.NewGuid():N}",
            };
            _contextMock.SteelGrades.Add(steelGrade);
            return steelGrade;
        }

        private SteelGrade CreateDummySteelGradeWithDetails(string name, string standard, int density)
        {
            var steelGrade = new SteelGrade
            {
                Id = Guid.NewGuid(),
                Name = name,
                Standard = standard,
                Density = density
            };
            _contextMock.SteelGrades.Add(steelGrade);
            return steelGrade;
        }

        // ─── GetSteelGradesAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetSteelGradesAsync_WhenExist_ReturnsSuccessStatusAndNotNullData()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var grade = CreateDummySteelGrade($"S355_{uniqueSuffix}");

            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _steelGradeServicesMock.GetSteelGradesAsync();

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.Any(g => g.Name == $"S355_{uniqueSuffix}")).IsTrue();
        }

        [Test]
        public async Task GetSteelGradesAsync_WhenEmpty_ReturnsSuccessStatusAndEmptyData()
        {
            var result = await _steelGradeServicesMock.GetSteelGradesAsync();

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data).IsEmpty();
        }

        // ─── GetSteelGradeList ──────────────────────────────────────────────────

        [Test]
        public async Task GetSteelGradeList_WhenStandardRequest_ReturnsPagedResult()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            CreateDummySteelGradeWithDetails($"1.4301_{uniqueSuffix}", "EN 10088", 79);
            CreateDummySteelGradeWithDetails($"1.4404_{uniqueSuffix}", "EN 10088", 80);
            await _contextMock.SaveChangesAsync();

            var command = new SteelGradeListCommand
            {
                PageNumber = 1,
                PageSize = 10,
                SearchTerm = uniqueSuffix
            };

            // Act
            var result = await _steelGradeServicesMock.GetSteelGradeList(command);

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
            CreateDummySteelGradeWithDetails($"GradeA_{uniqueSuffix}", "DIN 17100", 785);
            CreateDummySteelGradeWithDetails($"GradeB_{uniqueSuffix}", "SPECIAL_NORM", 795);
            await _contextMock.SaveChangesAsync();

            var command = new SteelGradeListCommand
            {
                PageNumber = 1,
                PageSize = 10,
                SearchTerm = "SPECIAL_NORM"
            };

            // Act
            var result = await _steelGradeServicesMock.GetSteelGradeList(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data!.Items.Count()).IsEqualTo(1);
            await Assert.That(result.Data.Items.First().Name).IsEqualTo($"GradeB_{uniqueSuffix}");
        }

        [Test]
        public async Task GetSteelGradeList_WhenSortingByDensityDescending_OrdersCorrectly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            CreateDummySteelGradeWithDetails($"LowDensity_{uniqueSuffix}", "STD", 71);
            CreateDummySteelGradeWithDetails($"HighDensity_{uniqueSuffix}", "STD", 85);
            await _contextMock.SaveChangesAsync();

            var command = new SteelGradeListCommand
            {
                PageNumber = 1,
                PageSize = 10,
                SearchTerm = uniqueSuffix,
                SortBy = "density",
                SortDescending = true
            };

            // Act
            var result = await _steelGradeServicesMock.GetSteelGradeList(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            var items = result.Data!.Items.ToList();
            await Assert.That(items.First().Name).IsEqualTo($"HighDensity_{uniqueSuffix}");
            await Assert.That(items.Last().Name).IsEqualTo($"LowDensity_{uniqueSuffix}");
        }

        [Test]
        public async Task GetSteelGradeList_WhenEmpty_ReturnsEmptyPagedResult()
        {
            // Arrange
            var command = new SteelGradeListCommand
            {
                PageNumber = 1,
                PageSize = 10,
                SearchTerm = "non_existing_steel_grade_name"
            };

            // Act
            var result = await _steelGradeServicesMock.GetSteelGradeList(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data!.Items).IsEmpty();
            await Assert.That(result.Data.TotalCount).IsEqualTo(0);
        }
    }
}
