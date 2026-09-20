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
    public class AnalyticsServiceTest
    {
        protected AppDbContext _contextMock = null!;

        private static PostgreSqlContainer _dbContainer = null!;
        private static string _connectionString = null!;
        private string _currentSchema = null!;

        protected AnalyticsService _analyticsServiceMock = null!;
        protected ILogger<AnalyticsService> _loggerMock = null!;

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

            _loggerMock = new LoggerFactory().CreateLogger<AnalyticsService>();

            _analyticsServiceMock = new AnalyticsService(_contextMock, _loggerMock);
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

        private async Task<(ApplicationUser User, Currency Currency, Company Company, Contact Contact)> SeedBaseEntitiesAsync()
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                FirstName = "Adam",
                LastName = "Kowalski",
                Email = "adam.kowalski@stal-crm.pl",
                UserName = "adam.kowalski@stal-crm.pl"
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Polski Złoty",
                Code = "PLN",
                DecimalPlaces = 2
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = "Hurtownia Stali StalMet Sp. z o.o.",
                NIP = "1234567890",
                OwnerId = user.Id
            };

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Piotr",
                LastName = "Nowak",
                CompanyId = company.Id,
                OwnerId = user.Id,
                IsPrimary = true
            };

            _contextMock.Users.Add(user);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.Add(contact);
            await _contextMock.SaveChangesAsync();

            return (user, currency, company, contact);
        }

        // ─── GetTeamKpiSummaryAsync ─────────────────────────────────────────────────


        [Test]
        public async Task GetTeamKpiSummaryAsync_WhenDatabaseIsEmpty_ShouldReturnZeroes()
        {
            // Act
            var result = await _analyticsServiceMock.GetTeamKpiSummaryAsync();

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();

            var data = result.Data!;
            await Assert.That(data.RevenueThisWeek).IsEqualTo(0m);
            await Assert.That(data.RevenueThisMonth).IsEqualTo(0m);
            await Assert.That(data.RevenueThisYear).IsEqualTo(0m);
            await Assert.That(data.ActiveDealsCount).IsEqualTo(0);
            await Assert.That(data.WonDealsThisMonth).IsEqualTo(0);
            await Assert.That(data.LostDealsThisMonth).IsEqualTo(0);
            await Assert.That(data.CompletedTasksThisMonth).IsEqualTo(0);
            await Assert.That(data.PendingTasksCount).IsEqualTo(0);
            await Assert.That(data.OverdueTasksCount).IsEqualTo(0);
        }

        [Test]
        public async Task GetTeamKpiSummaryAsync_WhenDealsExist_ShouldProperlyAggregateFinancialsAndDealStatusCounts()
        {
            // Arrange
            var (user, currency, company, contact) = await SeedBaseEntitiesAsync();
            var nowUtc = DateTime.UtcNow;

            int diffToMonday = (7 + ((int)nowUtc.DayOfWeek - (int)DayOfWeek.Monday)) % 7;
            var startOfWeek = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, 0, 0, 0, DateTimeKind.Utc).AddDays(-diffToMonday);
            var startOfMonth = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var startOfYear = new DateTime(nowUtc.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var deals = new List<Deal>
            {
                new Deal
                {
                    Id = Guid.NewGuid(),
                    Name = "D/2026/01/0001",
                    Value = 1_000_000_000L,
                    Status = DealsStatusEnum.Complete,
                    CloseDate = startOfWeek.AddHours(3),
                    OwnerId = user.Id,
                    CurrencyId = currency.Id,
                    CompanyId = company.Id,
                    ContactId = contact.Id
                },
                new Deal
                {
                    Id = Guid.NewGuid(),
                    Name = "D/2026/01/0002",
                    Value = 500_000_000L,
                    Status = DealsStatusEnum.Complete,
                    CloseDate = startOfMonth.AddMinutes(10),
                    OwnerId = user.Id,
                    CurrencyId = currency.Id,
                    CompanyId = company.Id,
                    ContactId = contact.Id
                },
                new Deal
                {
                    Id = Guid.NewGuid(),
                    Name = "D/2026/01/0003",
                    Value = 250_000_000L,
                    Status = DealsStatusEnum.Complete,
                    CloseDate = startOfYear.AddMinutes(10),
                    OwnerId = user.Id,
                    CurrencyId = currency.Id,
                    CompanyId = company.Id,
                    ContactId = contact.Id
                },
                new Deal
                {
                    Id = Guid.NewGuid(),
                    Name = "D/2025/12/0001",
                    Value = 800_000_000L,
                    Status = DealsStatusEnum.Complete,
                    CloseDate = startOfYear.AddDays(-5),
                    OwnerId = user.Id,
                    CurrencyId = currency.Id,
                    CompanyId = company.Id,
                    ContactId = contact.Id
                },
                new Deal
                {
                    Id = Guid.NewGuid(),
                    Name = "D/2026/01/0004",
                    Value = 400_000_000L,
                    Status = DealsStatusEnum.Cancelled,
                    CloseDate = startOfMonth.AddHours(2),
                    OwnerId = user.Id,
                    CurrencyId = currency.Id,
                    CompanyId = company.Id,
                    ContactId = contact.Id
                },
                new Deal
                {
                    Id = Guid.NewGuid(),
                    Name = "D/2026/01/0005",
                    Value = 300_000_000L,
                    Status = DealsStatusEnum.ToDo,
                    CloseDate = nowUtc.AddDays(10),
                    OwnerId = user.Id,
                    CurrencyId = currency.Id,
                    CompanyId = company.Id,
                    ContactId = contact.Id
                },
                new Deal
                {
                    Id = Guid.NewGuid(),
                    Name = "D/2026/01/0006",
                    Value = 120_000_000L,
                    Status = DealsStatusEnum.ToDo,
                    CloseDate = nowUtc.AddDays(15),
                    OwnerId = user.Id,
                    CurrencyId = currency.Id,
                    CompanyId = company.Id,
                    ContactId = contact.Id
                }
            };

            _contextMock.Deals.AddRange(deals);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _analyticsServiceMock.GetTeamKpiSummaryAsync();

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            var data = result.Data!;

            await Assert.That(data.RevenueThisYear).IsEqualTo(175_000.00m);
            await Assert.That(data.WonDealsThisMonth).IsEqualTo(2);
            await Assert.That(data.LostDealsThisMonth).IsEqualTo(1);
            await Assert.That(data.ActiveDealsCount).IsEqualTo(2);
        }

        [Test]
        public async Task GetTeamKpiSummaryAsync_WhenTasksExist_ShouldProperlyCountTaskMetrics()
        {
            // Arrange
            var (user, _, _, contact) = await SeedBaseEntitiesAsync();
            var nowUtc = DateTime.UtcNow;
            var startOfMonth = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            var tasks = new List<Tasks>
            {
                new Tasks
                {
                    Id = Guid.NewGuid(),
                    Title = "Wysłanie atestu hutniczego",
                    Description = "Wysłano certyfikat 3.1",
                    Status = TaskStatusEnum.Complete,
                    Priority = TaskPriorityEnum.High,
                    DueAt = startOfMonth.AddDays(1),
                    AssignedToId = user.Id,
                    CreatedById = user.Id,
                    ContactId = contact.Id
                },
                new Tasks
                {
                    Id = Guid.NewGuid(),
                    Title = "Kontakt z kierownikiem budowy",
                    Description = "Telefon w sprawie dostawy",
                    Status = TaskStatusEnum.InProgress,
                    Priority = TaskPriorityEnum.High,
                    DueAt = nowUtc.AddDays(-1),
                    AssignedToId = user.Id,
                    CreatedById = user.Id,
                    ContactId = contact.Id
                },
                new Tasks
                {
                    Id = Guid.NewGuid(),
                    Title = "Wycena profili HEB 200",
                    Description = "Przygotowanie oferty",
                    Status = TaskStatusEnum.ToDo,
                    Priority = TaskPriorityEnum.Medium,
                    DueAt = nowUtc.AddDays(3),
                    AssignedToId = user.Id,
                    CreatedById = user.Id,
                    ContactId = contact.Id
                },
                new Tasks
                {
                    Id = Guid.NewGuid(),
                    Title = "Weryfikacja limitu kupieckiego",
                    Description = "Oczekiwanie na raport z wywiadowni",
                    Status = TaskStatusEnum.Break,
                    Priority = TaskPriorityEnum.Low,
                    DueAt = nowUtc.AddDays(5),
                    AssignedToId = user.Id,
                    CreatedById = user.Id,
                    ContactId = contact.Id
                }
            };

            _contextMock.Tasks.AddRange(tasks);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _analyticsServiceMock.GetTeamKpiSummaryAsync();

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            var data = result.Data!;

            await Assert.That(data.CompletedTasksThisMonth).IsEqualTo(1);
            await Assert.That(data.PendingTasksCount).IsEqualTo(3);
            await Assert.That(data.OverdueTasksCount).IsEqualTo(1);
        }

        [Test]
        public async Task GetTeamKpiSummaryAsync_WhenSoftDeletedEntitiesExist_ShouldIgnoreDeletedEntities()
        {
            // Arrange
            var (user, currency, company, contact) = await SeedBaseEntitiesAsync();
            var nowUtc = DateTime.UtcNow;
            var startOfMonth = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            var deletedDeal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "D/DELETED/001",
                Value = 500_000_000L,
                Status = DealsStatusEnum.Complete,
                CloseDate = startOfMonth.AddDays(1),
                OwnerId = user.Id,
                CurrencyId = currency.Id,
                CompanyId = company.Id,
                ContactId = contact.Id,
                IsDeleted = true
            };

            var deletedTask = new Tasks
            {
                Id = Guid.NewGuid(),
                Title = "Usunięte zadanie",
                Description = "Opis",
                Status = TaskStatusEnum.ToDo,
                Priority = TaskPriorityEnum.Medium,
                DueAt = nowUtc.AddDays(-2),
                AssignedToId = user.Id,
                CreatedById = user.Id,
                IsDeleted = true
            };

            _contextMock.Deals.Add(deletedDeal);
            _contextMock.Tasks.Add(deletedTask);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _analyticsServiceMock.GetTeamKpiSummaryAsync();

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            var data = result.Data!;

            await Assert.That(data.RevenueThisMonth).IsEqualTo(0m);
            await Assert.That(data.WonDealsThisMonth).IsEqualTo(0);
            await Assert.That(data.PendingTasksCount).IsEqualTo(0);
            await Assert.That(data.OverdueTasksCount).IsEqualTo(0);
        }
    }
}
