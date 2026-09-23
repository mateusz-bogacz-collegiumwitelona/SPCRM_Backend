using Domain.Constants;
using Domain.Enum;
using Domain.Models;
using Infrastructure;
using Infrastructure.Interceptors;
using Infrastructure.Pdf;
using Infrastructure.Pdf.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Services.Accessors;
using Services.Command.Analytics;
using Services.Command.List;
using Services.Response.Analytics;
using Services.Services;
using System.Globalization;
using Testcontainers.PostgreSql;
using Tests.Services.Fakes;

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
        private IAnalyticsPdfGenerator _pdfMock = null!;
        private ICancellationTokenAccessor _ctMock = null!;

        [Before(Class)]
        [Obsolete]
        public static async Task SetupClassAsync()
        {
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

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

            _pdfMock = new AnalyticsPdfGenerator();

            _ctMock = new FakeCancellationTokenAccessor();

            _analyticsServiceMock = new AnalyticsService(_contextMock, _loggerMock, _pdfMock, _ctMock);
        }


        private static decimal GetAmount(IEnumerable<CurrencyAmountResponse?> revenues, string currencyCode = "PLN")
            => revenues?.FirstOrDefault(r => r?.CurrencyCode == currencyCode)?.Amount ?? 0m;

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
            await Assert.That(data.RevenueThisWeek).IsEmpty();
            await Assert.That(data.RevenueThisMonth).IsEmpty();
            await Assert.That(data.RevenueThisYear).IsEmpty();
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

            await Assert.That(GetAmount(data.RevenueThisYear)).IsEqualTo(175_000.00m);
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

            await Assert.That(data.RevenueThisMonth).IsEmpty();
            await Assert.That(data.WonDealsThisMonth).IsEqualTo(0);
            await Assert.That(data.PendingTasksCount).IsEqualTo(0);
            await Assert.That(data.OverdueTasksCount).IsEqualTo(0);
        }

        // ─── GetTeamRevenueChartAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetTeamRevenueChartAsync_WhenHalfYearPeriod_ShouldReturnSixMonthsAndAggregateCorrectly()
        {
            // Arrange
            var (user, currency, company, contact) = await SeedBaseEntitiesAsync();
            var nowUtc = DateTime.UtcNow;

            var currentMonthStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var threeMonthsAgo = currentMonthStart.AddMonths(-3);

            var deals = new List<Deal>
            {
                new Deal
                {
                    Id = Guid.NewGuid(),
                    Name = "D/WYKRES/01",
                    Value = 1_000_000_000L,
                    Status = DealsStatusEnum.Complete,
                    CloseDate = currentMonthStart.AddDays(2),
                    OwnerId = user.Id,
                    CurrencyId = currency.Id,
                    CompanyId = company.Id,
                    ContactId = contact.Id
                },
                new Deal
                {
                    Id = Guid.NewGuid(),
                    Name = "D/WYKRES/02",
                    Value = 500_000_000L,
                    Status = DealsStatusEnum.Complete,
                    CloseDate = threeMonthsAgo.AddDays(1),
                    OwnerId = user.Id,
                    CurrencyId = currency.Id,
                    CompanyId = company.Id,
                    ContactId = contact.Id
                },
                new Deal
                {
                    Id = Guid.NewGuid(),
                    Name = "D/WYKRES/03",
                    Value = 200_000_000L,
                    Status = DealsStatusEnum.Cancelled,
                    CloseDate = currentMonthStart.AddDays(3),
                    OwnerId = user.Id,
                    CurrencyId = currency.Id,
                    CompanyId = company.Id,
                    ContactId = contact.Id
                }
            };

            _contextMock.Deals.AddRange(deals);
            await _contextMock.SaveChangesAsync();

            var command = new AnalyticsChartCommand { Period = AnalyticsPeriodEnum.HalfYear };

            // Act
            var result = await _analyticsServiceMock.GetTeamRevenueChartAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();

            var chart = result.Data!;
            await Assert.That(chart.Count).IsEqualTo(6);

            var polishCulture = new CultureInfo("pl-PL");

            var latestMonthPoint = chart[^1];
            await Assert.That(latestMonthPoint.Label).IsEqualTo(nowUtc.ToString("MMM yyyy", polishCulture));
            await Assert.That(GetAmount(latestMonthPoint.Revenue)).IsEqualTo(100_000m);
            await Assert.That(latestMonthPoint.DealsWonCount).IsEqualTo(1);

            var threeMonthsAgoPoint = chart[2];
            await Assert.That(threeMonthsAgoPoint.Label).IsEqualTo(threeMonthsAgo.ToString("MMM yyyy", polishCulture));
            await Assert.That(GetAmount(threeMonthsAgoPoint.Revenue)).IsEqualTo(50_000m);
            await Assert.That(threeMonthsAgoPoint.DealsWonCount).IsEqualTo(1);

            var emptyMonthPoint = chart[0];
            await Assert.That(emptyMonthPoint.Revenue).IsEmpty();
            await Assert.That(emptyMonthPoint.DealsWonCount).IsEqualTo(0);
        }

        [Test]
        public async Task GetTeamRevenueChartAsync_WhenCurrentYearPeriod_ShouldReturnTwelveMonths()
        {
            // Arrange
            var (user, currency, company, contact) = await SeedBaseEntitiesAsync();
            var nowUtc = DateTime.UtcNow;

            var januaryFirst = new DateTime(nowUtc.Year, 1, 1, 12, 0, 0, DateTimeKind.Utc);

            var dealInJan = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "D/JAN/01",
                Value = 300_000_000L,
                Status = DealsStatusEnum.Complete,
                CloseDate = januaryFirst,
                OwnerId = user.Id,
                CurrencyId = currency.Id,
                CompanyId = company.Id,
                ContactId = contact.Id
            };

            _contextMock.Deals.Add(dealInJan);
            await _contextMock.SaveChangesAsync();

            var command = new AnalyticsChartCommand { Period = AnalyticsPeriodEnum.CurrentYear };

            // Act
            var result = await _analyticsServiceMock.GetTeamRevenueChartAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            var chart = result.Data!;

            await Assert.That(chart.Count).IsEqualTo(12);

            var janPoint = chart[0];
            await Assert.That(GetAmount(janPoint.Revenue)).IsEqualTo(30_000m);
            await Assert.That(janPoint.DealsWonCount).IsEqualTo(1);
        }

        [Test]
        public async Task GetTeamRevenueChartAsync_WhenCurrentMonthPeriod_ShouldReturnFourIntervals()
        {
            // Arrange
            var (user, currency, company, contact) = await SeedBaseEntitiesAsync();
            var nowUtc = DateTime.UtcNow;

            var dealFirstWeek = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "D/MONTH/01",
                Value = 150_000_000L,
                Status = DealsStatusEnum.Complete,
                CloseDate = new DateTime(nowUtc.Year, nowUtc.Month, 2, 10, 0, 0, DateTimeKind.Utc),
                OwnerId = user.Id,
                CurrencyId = currency.Id,
                CompanyId = company.Id,
                ContactId = contact.Id
            };

            _contextMock.Deals.Add(dealFirstWeek);
            await _contextMock.SaveChangesAsync();

            var command = new AnalyticsChartCommand { Period = AnalyticsPeriodEnum.CurrentMonth };

            // Act
            var result = await _analyticsServiceMock.GetTeamRevenueChartAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            var chart = result.Data!;

            await Assert.That(chart.Count).IsEqualTo(4);

            var firstWeek = chart[0];
            await Assert.That(firstWeek.Label).IsEqualTo("Dni 1-7");
            await Assert.That(GetAmount(firstWeek.Revenue)).IsEqualTo(15_000m);
            await Assert.That(firstWeek.DealsWonCount).IsEqualTo(1);
        }

        // ─── GetEmployeeKpiSummaryAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetEmployeeKpiSummaryAsync_WhenUserDoesNotExist_ShouldReturnNotFound()
        {
            // Arrange
            var nonExistingUserId = Guid.NewGuid();

            // Act
            var result = await _analyticsServiceMock.GetEmployeeKpiSummaryAsync(nonExistingUserId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.UserNotFound);
        }

        [Test]
        public async Task GetEmployeeKpiSummaryAsync_WhenUserIsSoftDeleted_ShouldReturnNotFound()
        {
            // Arrange
            var (user, _, _, _) = await SeedBaseEntitiesAsync();
            user.IsDeleted = true;
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _analyticsServiceMock.GetEmployeeKpiSummaryAsync(user.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.UserNotFound);
        }

        [Test]
        public async Task GetEmployeeKpiSummaryAsync_WhenMultipleUsersExist_ShouldIsolateEmployeeMetricsAndCalculateWinRate()
        {
            // Arrange
            var (targetUser, currency, company, contact) = await SeedBaseEntitiesAsync();

            var otherUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                FirstName = "Marek",
                LastName = "Zieliński",
                Email = "marek.zielinski@stal-crm.pl",
                UserName = "marek.zielinski@stal-crm.pl"
            };
            _contextMock.Users.Add(otherUser);
            await _contextMock.SaveChangesAsync();

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
                    Name = "D/TARGET/01",
                    Value = 800_000_000L,
                    Status = DealsStatusEnum.Complete,
                    CloseDate = startOfWeek.AddHours(2),
                    OwnerId = targetUser.Id,
                    CurrencyId = currency.Id,
                    CompanyId = company.Id,
                    ContactId = contact.Id
                },
                new Deal
                {
                    Id = Guid.NewGuid(),
                    Name = "D/TARGET/02",
                    Value = 400_000_000L,
                    Status = DealsStatusEnum.Complete,
                    CloseDate = startOfMonth.AddDays(1),
                    OwnerId = targetUser.Id,
                    CurrencyId = currency.Id,
                    CompanyId = company.Id,
                    ContactId = contact.Id
                },
                new Deal
                {
                    Id = Guid.NewGuid(),
                    Name = "D/TARGET/03",
                    Value = 300_000_000L,
                    Status = DealsStatusEnum.Complete,
                    CloseDate = startOfYear.AddMinutes(5),
                    OwnerId = targetUser.Id,
                    CurrencyId = currency.Id,
                    CompanyId = company.Id,
                    ContactId = contact.Id
                },
                new Deal
                {
                    Id = Guid.NewGuid(),
                    Name = "D/TARGET/04",
                    Value = 100_000_000L,
                    Status = DealsStatusEnum.Cancelled,
                    CloseDate = startOfMonth.AddDays(2),
                    OwnerId = targetUser.Id,
                    CurrencyId = currency.Id,
                    CompanyId = company.Id,
                    ContactId = contact.Id
                },
                new Deal
                {
                    Id = Guid.NewGuid(),
                    Name = "D/TARGET/05",
                    Value = 500_000_000L,
                    Status = DealsStatusEnum.ToDo,
                    CloseDate = nowUtc.AddDays(5),
                    OwnerId = targetUser.Id,
                    CurrencyId = currency.Id,
                    CompanyId = company.Id,
                    ContactId = contact.Id
                },
                new Deal
                {
                    Id = Guid.NewGuid(),
                    Name = "D/OTHER/01",
                    Value = 2_000_000_000L,
                    Status = DealsStatusEnum.Complete,
                    CloseDate = startOfMonth.AddDays(3),
                    OwnerId = otherUser.Id,
                    CurrencyId = currency.Id,
                    CompanyId = company.Id,
                    ContactId = contact.Id
                }
            };

            var tasks = new List<Tasks>
            {
                new Tasks
                {
                    Id = Guid.NewGuid(),
                    Title = "Spotkanie z klientem",
                    Description = "Dopięcie kontraktu",
                    Status = TaskStatusEnum.Complete,
                    Priority = TaskPriorityEnum.High,
                    DueAt = startOfMonth.AddDays(2),
                    AssignedToId = targetUser.Id,
                    CreatedById = targetUser.Id
                },
                new Tasks
                {
                    Id = Guid.NewGuid(),
                    Title = "Telefon ofertowy",
                    Description = "Kontakt ponowny",
                    Status = TaskStatusEnum.InProgress,
                    Priority = TaskPriorityEnum.Medium,
                    DueAt = nowUtc.AddDays(-1),
                    AssignedToId = targetUser.Id,
                    CreatedById = targetUser.Id
                },
                new Tasks
                {
                    Id = Guid.NewGuid(),
                    Title = "Zadanie innego handlowca",
                    Description = "Inny opis",
                    Status = TaskStatusEnum.InProgress,
                    Priority = TaskPriorityEnum.High,
                    DueAt = nowUtc.AddDays(-1),
                    AssignedToId = otherUser.Id,
                    CreatedById = otherUser.Id
                }
            };

            _contextMock.Deals.AddRange(deals);
            _contextMock.Tasks.AddRange(tasks);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _analyticsServiceMock.GetEmployeeKpiSummaryAsync(targetUser.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var data = result.Data!;

            await Assert.That(GetAmount(data.RevenueThisWeek)).IsEqualTo(80_000m);
            await Assert.That(GetAmount(data.RevenueThisMonth)).IsEqualTo(120_000m);
            await Assert.That(GetAmount(data.RevenueThisYear)).IsEqualTo(150_000m);
            await Assert.That(data.ActiveDealsCount).IsEqualTo(1);
            await Assert.That(GetAmount(data.ActiveDealsPipelineValue)).IsEqualTo(50_000m);
            await Assert.That(data.WonDealsThisMonth).IsEqualTo(2);
            await Assert.That(data.LostDealsThisMonth).IsEqualTo(1);
            await Assert.That(data.WinRatePercentageThisMonth).IsEqualTo(66.67m);
            await Assert.That(data.CompletedTasksThisMonth).IsEqualTo(1);
            await Assert.That(data.PendingTasksCount).IsEqualTo(1);
            await Assert.That(data.OverdueTasksCount).IsEqualTo(1);
        }

        [Test]
        public async Task GetEmployeeKpiSummaryAsync_WhenNoDealsClosed_ShouldReturnZeroWinRateWithoutDivisionByZero()
        {
            // Arrange
            var (user, _, _, _) = await SeedBaseEntitiesAsync();

            // Act
            var result = await _analyticsServiceMock.GetEmployeeKpiSummaryAsync(user.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            var data = result.Data!;
            await Assert.That(data.WonDealsThisMonth).IsEqualTo(0);
            await Assert.That(data.LostDealsThisMonth).IsEqualTo(0);
            await Assert.That(data.WinRatePercentageThisMonth).IsEqualTo(0m);
            await Assert.That(data.ActiveDealsPipelineValue).IsEmpty();
        }

        // ─── GetEmployeeRevenueChartAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetEmployeeRevenueChartAsync_WhenUserDoesNotExist_ShouldReturnNotFound()
        {
            // Arrange
            var nonExistingUserId = Guid.NewGuid();
            var command = new AnalyticsChartCommand { Period = AnalyticsPeriodEnum.HalfYear };

            // Act
            var result = await _analyticsServiceMock.GetEmployeeRevenueChartAsync(nonExistingUserId, command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.UserNotFound);
        }

        [Test]
        public async Task GetEmployeeRevenueChartAsync_WhenUserIsSoftDeleted_ShouldReturnNotFound()
        {
            // Arrange
            var (user, _, _, _) = await SeedBaseEntitiesAsync();
            user.IsDeleted = true;
            await _contextMock.SaveChangesAsync();

            var command = new AnalyticsChartCommand { Period = AnalyticsPeriodEnum.HalfYear };

            // Act
            var result = await _analyticsServiceMock.GetEmployeeRevenueChartAsync(user.Id, command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.UserNotFound);
        }

        [Test]
        public async Task GetEmployeeRevenueChartAsync_WhenDealsExist_ShouldIsolateEmployeeDataAndAggregateCorrectly()
        {
            // Arrange
            var (targetUser, currency, company, contact) = await SeedBaseEntitiesAsync();

            var otherUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                FirstName = "Tomasz",
                LastName = "Kowalski",
                Email = "tomasz.kowalski@stal-crm.pl",
                UserName = "tomasz.kowalski@stal-crm.pl"
            };
            _contextMock.Users.Add(otherUser);
            await _contextMock.SaveChangesAsync();

            var nowUtc = DateTime.UtcNow;
            var currentMonthStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var twoMonthsAgo = currentMonthStart.AddMonths(-2);

            var deals = new List<Deal>
            {
                new Deal
                {
                    Id = Guid.NewGuid(),
                    Name = "D/EMP_CHART/01",
                    Value = 600_000_000L,
                    Status = DealsStatusEnum.Complete,
                    CloseDate = currentMonthStart.AddDays(2),
                    OwnerId = targetUser.Id,
                    CurrencyId = currency.Id,
                    CompanyId = company.Id,
                    ContactId = contact.Id
                },
                new Deal
                {
                    Id = Guid.NewGuid(),
                    Name = "D/EMP_CHART/02",
                    Value = 400_000_000L,
                    Status = DealsStatusEnum.Complete,
                    CloseDate = twoMonthsAgo.AddDays(5),
                    OwnerId = targetUser.Id,
                    CurrencyId = currency.Id,
                    CompanyId = company.Id,
                    ContactId = contact.Id
                },
                new Deal
                {
                    Id = Guid.NewGuid(),
                    Name = "D/EMP_CHART/03",
                    Value = 100_000_000L,
                    Status = DealsStatusEnum.Cancelled,
                    CloseDate = currentMonthStart.AddDays(3),
                    OwnerId = targetUser.Id,
                    CurrencyId = currency.Id,
                    CompanyId = company.Id,
                    ContactId = contact.Id
                },
                new Deal
                {
                    Id = Guid.NewGuid(),
                    Name = "D/OTHER_CHART/01",
                    Value = 1_500_000_000L,
                    Status = DealsStatusEnum.Complete,
                    CloseDate = currentMonthStart.AddDays(2),
                    OwnerId = otherUser.Id,
                    CurrencyId = currency.Id,
                    CompanyId = company.Id,
                    ContactId = contact.Id
                }
            };

            _contextMock.Deals.AddRange(deals);
            await _contextMock.SaveChangesAsync();

            var command = new AnalyticsChartCommand { Period = AnalyticsPeriodEnum.HalfYear };

            // Act
            var result = await _analyticsServiceMock.GetEmployeeRevenueChartAsync(targetUser.Id, command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();

            var chart = result.Data!;
            await Assert.That(chart.Count).IsEqualTo(6);

            var polishCulture = new CultureInfo("pl-PL");

            var currentMonthPoint = chart[^1];
            await Assert.That(currentMonthPoint.Label).IsEqualTo(nowUtc.ToString("MMM yyyy", polishCulture));
            await Assert.That(GetAmount(currentMonthPoint.Revenue)).IsEqualTo(60_000m);
            await Assert.That(currentMonthPoint.DealsWonCount).IsEqualTo(1);

            var twoMonthsAgoPoint = chart[3];
            await Assert.That(twoMonthsAgoPoint.Label).IsEqualTo(twoMonthsAgo.ToString("MMM yyyy", polishCulture));
            await Assert.That(GetAmount(twoMonthsAgoPoint.Revenue)).IsEqualTo(40_000m);
            await Assert.That(twoMonthsAgoPoint.DealsWonCount).IsEqualTo(1);

            var emptyMonthPoint = chart[0];
            await Assert.That(emptyMonthPoint.Revenue).IsEmpty();
            await Assert.That(emptyMonthPoint.DealsWonCount).IsEqualTo(0);
        }

        [Test]
        public async Task GetEmployeeRevenueChartAsync_WhenCurrentMonth_ShouldReturnFourWeeklyIntervals()
        {
            // Arrange
            var (targetUser, currency, company, contact) = await SeedBaseEntitiesAsync();
            var nowUtc = DateTime.UtcNow;

            var dealFirstInterval = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "D/EMP_MONTH/01",
                Value = 250_000_000L,
                Status = DealsStatusEnum.Complete,
                CloseDate = new DateTime(nowUtc.Year, nowUtc.Month, 3, 10, 0, 0, DateTimeKind.Utc),
                OwnerId = targetUser.Id,
                CurrencyId = currency.Id,
                CompanyId = company.Id,
                ContactId = contact.Id
            };

            _contextMock.Deals.Add(dealFirstInterval);
            await _contextMock.SaveChangesAsync();

            var command = new AnalyticsChartCommand { Period = AnalyticsPeriodEnum.CurrentMonth };

            // Act
            var result = await _analyticsServiceMock.GetEmployeeRevenueChartAsync(targetUser.Id, command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            var chart = result.Data!;

            await Assert.That(chart.Count).IsEqualTo(4);

            var firstInterval = chart[0];
            await Assert.That(firstInterval.Label).IsEqualTo("Dni 1-7");
            await Assert.That(GetAmount(firstInterval.Revenue)).IsEqualTo(25_000m);
            await Assert.That(firstInterval.DealsWonCount).IsEqualTo(1);
        }

        // ─── GetTeamLeaderboardAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetTeamLeaderboardAsync_WhenUsersExist_ShouldRankByRevenueDescendingAndCalculateMetrics()
        {
            // Arrange
            var nowUtc = DateTime.UtcNow;
            var startOfMonthUtc = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Polski Złoty",
                Code = "PLN",
                DecimalPlaces = 2
            };

            var user1 = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = "jan@example.com",
                UserName = "jan@example.com"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = "Leaderboard Sp. z o.o.",
                NIP = "9999999999",
                OwnerId = user1.Id
            };

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Michał",
                LastName = "Nowicki",
                CompanyId = company.Id,
                IsPrimary = true,
                OwnerId = user1.Id
            };

            var user2 = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                FirstName = "Anna",
                LastName = "Nowak",
                Email = "anna@example.com",
                UserName = "anna@example.com"
            };

            var user3 = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                FirstName = "Piotr",
                LastName = "Wiśniewski",
                Email = "piotr@example.com",
                UserName = "piotr@example.com"
            };

            _contextMock.Currencies.Add(currency);
            _contextMock.Users.AddRange(user1, user2, user3);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.Add(contact);
            await _contextMock.SaveChangesAsync();

            var deals = new List<Deal>
            {
                new Deal
                {
                    Id = Guid.NewGuid(),
                    Name = "D/U1/01",
                    Value = 500_000_000L,
                    Status = DealsStatusEnum.Complete,
                    CloseDate = startOfMonthUtc.AddDays(2),
                    OwnerId = user1.Id,
                    CurrencyId = currency.Id,
                    CompanyId = company.Id,
                    ContactId = contact.Id
                },
                new Deal
                {
                    Id = Guid.NewGuid(),
                    Name = "D/U1/02",
                    Value = 200_000_000L,
                    Status = DealsStatusEnum.Cancelled,
                    CloseDate = startOfMonthUtc.AddDays(3),
                    OwnerId = user1.Id,
                    CurrencyId = currency.Id,
                    CompanyId = company.Id,
                    ContactId = contact.Id
                },
                new Deal
                {
                    Id = Guid.NewGuid(),
                    Name = "D/U2/01",
                    Value = 1_000_000_000L,
                    Status = DealsStatusEnum.Complete,
                    CloseDate = startOfMonthUtc.AddDays(1),
                    OwnerId = user2.Id,
                    CurrencyId = currency.Id,
                    CompanyId = company.Id,
                    ContactId = contact.Id
                },
                new Deal
                {
                    Id = Guid.NewGuid(),
                    Name = "D/U2/02",
                    Value = 500_000_000L,
                    Status = DealsStatusEnum.Complete,
                    CloseDate = startOfMonthUtc.AddDays(5),
                    OwnerId = user2.Id,
                    CurrencyId = currency.Id,
                    CompanyId = company.Id,
                    ContactId = contact.Id
                },
                new Deal
                {
                    Id = Guid.NewGuid(),
                    Name = "D/U3/01",
                    Value = 300_000_000L,
                    Status = DealsStatusEnum.ToDo,
                    CloseDate = nowUtc.AddDays(10),
                    OwnerId = user3.Id,
                    CurrencyId = currency.Id,
                    CompanyId = company.Id,
                    ContactId = contact.Id
                }
            };

            _contextMock.Deals.AddRange(deals);
            await _contextMock.SaveChangesAsync();

            var command = new PaggedCommand { PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _analyticsServiceMock.GetTeamLeaderboardAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();

            var pagedData = result.Data!;
            await Assert.That(pagedData.TotalCount).IsEqualTo(3);

            var items = pagedData.Items.ToList();

            await Assert.That(items.Count).IsEqualTo(3);

            await Assert.That(items[0].EmployeeId).IsEqualTo(user2.Id);
            await Assert.That(GetAmount(items[0].RevenueThisMonth)).IsEqualTo(150_000m);
            await Assert.That(items[0].WonDealsThisMonth).IsEqualTo(2);
            await Assert.That(items[0].WinRatePercentageThisMonth).IsEqualTo(100m);

            await Assert.That(items[1].EmployeeId).IsEqualTo(user1.Id);
            await Assert.That(GetAmount(items[1].RevenueThisMonth)).IsEqualTo(50_000m);
            await Assert.That(items[1].WonDealsThisMonth).IsEqualTo(1);
            await Assert.That(items[1].WinRatePercentageThisMonth).IsEqualTo(50m);

            await Assert.That(items[2].EmployeeId).IsEqualTo(user3.Id);
            await Assert.That(items[2].RevenueThisMonth).IsEmpty();
            await Assert.That(items[2].WonDealsThisMonth).IsEqualTo(0);
            await Assert.That(items[2].ActiveDealsCount).IsEqualTo(1);
            await Assert.That(items[2].WinRatePercentageThisMonth).IsEqualTo(0m);
        }

        [Test]
        public async Task GetTeamLeaderboardAsync_WhenUserIsSoftDeleted_ShouldExcludeDeletedUser()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                FirstName = "Krzysztof",
                LastName = "Usunięty",
                Email = "krzysztof@example.com",
                UserName = "krzysztof@example.com",
                IsDeleted = true
            };

            _contextMock.Users.Add(user);
            await _contextMock.SaveChangesAsync();

            var command = new PaggedCommand { PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _analyticsServiceMock.GetTeamLeaderboardAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data!.TotalCount).IsEqualTo(0);
            await Assert.That(result.Data.Items).IsEmpty();
        }

        [Test]
        public async Task GetTeamLeaderboardAsync_WhenPaginationApplied_ShouldReturnCorrectPage()
        {
            // Arrange
            for (int i = 1; i <= 5; i++)
            {
                _contextMock.Users.Add(new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    FirstName = $"Pracownik{i}",
                    LastName = "Testowy",
                    Email = $"pracownik{i}@example.com",
                    UserName = $"pracownik{i}@example.com"
                });
            }
            await _contextMock.SaveChangesAsync();

            var command = new PaggedCommand { PageNumber = 2, PageSize = 2 };

            // Act
            var result = await _analyticsServiceMock.GetTeamLeaderboardAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            var data = result.Data!;

            await Assert.That(data.TotalCount).IsEqualTo(5);
            await Assert.That(data.Items.Count).IsEqualTo(2);
            await Assert.That(data.PageNumber).IsEqualTo(2);
            await Assert.That(data.TotalPages).IsEqualTo(3);
        }

        // ─── GenerateEmployeeReportPdfAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GenerateEmployeeReportPdfAsync_WhenUserDoesNotExist_ShouldReturnNotFound()
        {
            // Arrange
            var nonExistingUserId = Guid.NewGuid();
            var command = new AnalyticsChartCommand { Period = AnalyticsPeriodEnum.CurrentYear };

            // Act
            var result = await _analyticsServiceMock.GenerateEmployeeReportPdfAsync(nonExistingUserId, command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.UserNotFound);
        }

        [Test]
        public async Task GenerateEmployeeReportPdfAsync_WhenUserIsSoftDeleted_ShouldReturnNotFound()
        {
            // Arrange
            var (user, _, _, _) = await SeedBaseEntitiesAsync();
            user.IsDeleted = true;
            await _contextMock.SaveChangesAsync();

            var command = new AnalyticsChartCommand { Period = AnalyticsPeriodEnum.HalfYear };

            // Act
            var result = await _analyticsServiceMock.GenerateEmployeeReportPdfAsync(user.Id, command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.UserNotFound);
        }

        [Test]
        public async Task GenerateEmployeeReportPdfAsync_WhenDataValid_ShouldGeneratePdfFileSuccessfully()
        {
            // Arrange
            var (user, currency, company, contact) = await SeedBaseEntitiesAsync();
            var nowUtc = DateTime.UtcNow;

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "D/PDF/01",
                Value = 750_000_000L,
                Status = DealsStatusEnum.Complete,
                CloseDate = nowUtc,
                OwnerId = user.Id,
                CurrencyId = currency.Id,
                CompanyId = company.Id,
                ContactId = contact.Id
            };

            var task = new Tasks
            {
                Id = Guid.NewGuid(),
                Title = "Przygotowanie oferty rocznej",
                Description = "PDF raport test",
                Status = TaskStatusEnum.Complete,
                Priority = TaskPriorityEnum.High,
                DueAt = nowUtc,
                AssignedToId = user.Id,
                CreatedById = user.Id
            };

            _contextMock.Deals.Add(deal);
            _contextMock.Tasks.Add(task);
            await _contextMock.SaveChangesAsync();

            var command = new AnalyticsChartCommand { Period = AnalyticsPeriodEnum.CurrentYear };

            // Act
            var result = await _analyticsServiceMock.GenerateEmployeeReportPdfAsync(user.Id, command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();

            var pdfResponse = result.Data!;
            await Assert.That(pdfResponse.ContentType).IsEqualTo("application/pdf");
            await Assert.That(pdfResponse.FileContents).IsNotNull();
            await Assert.That(pdfResponse.FileContents.Length).IsGreaterThan(0);

            var safeLastName = user.LastName.Replace(" ", "_");
            await Assert.That(pdfResponse.FileName).StartsWith($"Raport_{safeLastName}_{command.Period}_");
            await Assert.That(pdfResponse.FileName).EndsWith(".pdf");
        }

        // ─── GenerateTeamReportPdfAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GenerateTeamReportPdfAsync_WhenDatabaseIsEmpty_ShouldGeneratePdfSuccessfullyWithZeroes()
        {
            // Arrange
            var command = new AnalyticsChartCommand { Period = AnalyticsPeriodEnum.CurrentYear };

            // Act
            var result = await _analyticsServiceMock.GenerateTeamReportPdfAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();

            var pdfResponse = result.Data!;
            await Assert.That(pdfResponse.ContentType).IsEqualTo("application/pdf");
            await Assert.That(pdfResponse.FileContents).IsNotNull();
            await Assert.That(pdfResponse.FileContents.Length).IsGreaterThan(0);
            await Assert.That(pdfResponse.FileName).StartsWith($"Raport_Zespolu_{command.Period}_");
            await Assert.That(pdfResponse.FileName).EndsWith(".pdf");
        }

        [Test]
        public async Task GenerateTeamReportPdfAsync_WhenDataValid_ShouldGenerateTeamPdfReportWithLeaderboardAndChart()
        {
            // Arrange
            var (user1, currency, company, contact) = await SeedBaseEntitiesAsync();

            var user2 = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                FirstName = "Monika",
                LastName = "Kaczmarek",
                Email = "monika.kaczmarek@stal-crm.pl",
                UserName = "monika.kaczmarek@stal-crm.pl"
            };

            _contextMock.Users.Add(user2);
            await _contextMock.SaveChangesAsync();

            var nowUtc = DateTime.UtcNow;
            var startOfMonthUtc = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            var deals = new List<Deal>
            {
                new Deal
                {
                    Id = Guid.NewGuid(),
                    Name = "D/TEAM_PDF/01",
                    Value = 1_200_000_000L,
                    Status = DealsStatusEnum.Complete,
                    CloseDate = startOfMonthUtc.AddDays(2),
                    OwnerId = user1.Id,
                    CurrencyId = currency.Id,
                    CompanyId = company.Id,
                    ContactId = contact.Id
                },
                new Deal
                {
                    Id = Guid.NewGuid(),
                    Name = "D/TEAM_PDF/02",
                    Value = 800_000_000L,
                    Status = DealsStatusEnum.Complete,
                    CloseDate = startOfMonthUtc.AddDays(4),
                    OwnerId = user2.Id,
                    CurrencyId = currency.Id,
                    CompanyId = company.Id,
                    ContactId = contact.Id
                },
                new Deal
                {
                    Id = Guid.NewGuid(),
                    Name = "D/TEAM_PDF/03",
                    Value = 300_000_000L,
                    Status = DealsStatusEnum.Cancelled,
                    CloseDate = startOfMonthUtc.AddDays(5),
                    OwnerId = user2.Id,
                    CurrencyId = currency.Id,
                    CompanyId = company.Id,
                    ContactId = contact.Id
                }
            };

            var tasks = new List<Tasks>
            {
                new Tasks
                {
                    Id = Guid.NewGuid(),
                    Title = "Wizyta handlowa na budowie",
                    Status = TaskStatusEnum.Complete,
                    Priority = TaskPriorityEnum.High,
                    DueAt = startOfMonthUtc.AddDays(3),
                    AssignedToId = user1.Id,
                    CreatedById = user1.Id,
                    Description = "Tak tak tam w lustrze to niestety ja"
                },
                new Tasks
                {
                    Id = Guid.NewGuid(),
                    Title = "Przygotowanie specyfikacji technicznej",
                    Status = TaskStatusEnum.ToDo,
                    Priority = TaskPriorityEnum.Medium,
                    DueAt = nowUtc.AddDays(-2),
                    AssignedToId = user2.Id,
                    CreatedById = user2.Id,
                    Description = "A bo ja wiem"
                }
            };

            _contextMock.Deals.AddRange(deals);
            _contextMock.Tasks.AddRange(tasks);
            await _contextMock.SaveChangesAsync();

            var command = new AnalyticsChartCommand { Period = AnalyticsPeriodEnum.HalfYear };

            // Act
            var result = await _analyticsServiceMock.GenerateTeamReportPdfAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();

            var pdfResponse = result.Data!;
            await Assert.That(pdfResponse.ContentType).IsEqualTo("application/pdf");
            await Assert.That(pdfResponse.FileContents).IsNotNull();
            await Assert.That(pdfResponse.FileContents.Length).IsGreaterThan(0);
            await Assert.That(pdfResponse.FileName).StartsWith($"Raport_Zespolu_{command.Period}_");
            await Assert.That(pdfResponse.FileName).EndsWith(".pdf");
        }
    }
}
