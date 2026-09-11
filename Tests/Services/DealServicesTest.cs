using Domain.Constants;
using Domain.Enum;
using Domain.Exceptions.Exception;
using Domain.Models;
using Domain.State;
using Infrastructure;
using Infrastructure.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Services.Command.Company;
using Services.Command.Deal;
using Services.Command.Product;
using Services.Command.Sales;
using Services.Factory;
using Services.Factory.Interfaces;
using Services.Interfaces;
using Services.Services;
using Testcontainers.PostgreSql;

namespace Tests.Services
{
    public class DealServicesTest
    {
        protected AppDbContext _contextMock = null!;

        private static PostgreSqlContainer _dbContainer = null!;
        private static string _connectionString = null!;

        protected DealServices _dealServicesMock = null!;
        protected ILogger<DealServices> _loggerMock = null!;

        private string _currentSchema = null!;
        protected IEntityAuthorizationService _entityAuthMock = null!;
        protected IDealStateMachineFactory _stateMock = null!;

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

            _loggerMock = new LoggerFactory().CreateLogger<DealServices>();

            _entityAuthMock = new EntityAuthorizationService(_contextMock);

            _stateMock = new DealStateMachineFactory();

            _dealServicesMock = new DealServices(
                _contextMock, 
                _loggerMock, 
                _entityAuthMock,
                _stateMock);
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


        private async Task<(Company Company, ApplicationUser User, Currency Currency)> SeedCompanyAndUserAsync()
        {
            var unique = Guid.NewGuid().ToString("N")[..8];
            var userId = Guid.NewGuid();

            var user = new ApplicationUser
            {
                Id = userId,
                UserName = $"User_{unique}",
                NormalizedUserName = $"USER_{unique}",
                Email = $"user_{unique}@test.com",
                NormalizedEmail = $"USER_{unique}@TEST.COM",
                FirstName = "Jan",
                LastName = "Kowalski"
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
                Name = $"Firma_{unique}",
                NIP = "5252525252",
                OwnerId = userId,
                Owner = user
            };

            _contextMock.Users.Add(user);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            return (company, user, currency);
        }

        // ─── GetDealsAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetDealsAsync_FiltersByOwnerAndMapsPropertiesCorrectly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var targetUserId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();

            var targetUser = new ApplicationUser
            {
                Id = targetUserId,
                UserName = $"Target_{uniqueSuffix}",
                NormalizedUserName = $"TARGET_{uniqueSuffix}",
                Email = $"t_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"T_{uniqueSuffix}@T.PL",
                FirstName = "Target",
                LastName = "User"
            };

            var otherUser = new ApplicationUser
            {
                Id = otherUserId,
                UserName = $"Other_{uniqueSuffix}",
                NormalizedUserName = $"OTHER_{uniqueSuffix}",
                Email = $"o_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"O_{uniqueSuffix}@T.PL",
                FirstName = "Other",
                LastName = "User"
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "PLN",
                Code = "PLN",
                DecimalPlaces = 2
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Company_{uniqueSuffix}",
                NIP = "1234567890",
                OwnerId = targetUserId,
                Owner = targetUser
            };

            var targetDeal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Target Deal",
                Value = 1500000,
                Status = DealsStatusEnum.InProgress,
                CloseDate = DateTime.UtcNow,
                CompanyId = company.Id,
                Company = company,
                OwnerId = targetUserId,
                Owner = targetUser,
                CurrencyId = currency.Id,
                Currency = currency
            };

            var otherDeal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Other Deal",
                Value = 3000000,
                Status = DealsStatusEnum.Complete,
                CloseDate = DateTime.UtcNow,
                CompanyId = company.Id,
                Company = company,
                OwnerId = otherUserId,
                Owner = otherUser,
                CurrencyId = currency.Id,
                Currency = currency
            };

            _contextMock.Users.AddRange(targetUser, otherUser);
            _contextMock.Companies.Add(company);
            _contextMock.Currencies.Add(currency);
            _contextMock.Deals.AddRange(targetDeal, otherDeal);
            await _contextMock.SaveChangesAsync();

            var command = new DealListCommand
            {
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _dealServicesMock.GetDealsAsync(command, targetUserId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var items = result.Data!.Items;

            await Assert.That(items).Count().IsEqualTo(1);

            var mappedDeal = items.First();

            await Assert.That(mappedDeal.Id).IsEqualTo(targetDeal.Id);
            await Assert.That(mappedDeal.Value).IsEqualTo(1500000L);
            await Assert.That(mappedDeal.Currency).IsEqualTo("PLN");
            await Assert.That(mappedDeal.DecimalPlace).IsEqualTo(2);
            await Assert.That(mappedDeal.CompanyName).IsEqualTo(company.Name);
            await Assert.That(mappedDeal.Nip).IsEqualTo(company.NIP);
            await Assert.That(mappedDeal.Status).IsEqualTo(targetDeal.Status.ToString());
            await Assert.That(mappedDeal.OwnerId).IsEqualTo(targetUserId);
            await Assert.That(mappedDeal.OwnerFirstName).IsEqualTo("Target");
            await Assert.That(mappedDeal.OwnerLastName).IsEqualTo("User");
        }

        [Test]
        public async Task GetDealsAsync_WhenUserHasNoSales_ReturnsEmptyListWithSuccessStatus()
        {
            // Arrange
            var randomUserId = Guid.NewGuid();

            var command = new DealListCommand { PageNumber = 1, PageSize = 10 };

            // Act 
            var result = await _dealServicesMock.GetDealsAsync(command, randomUserId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.Items).IsEmpty();
        }

        // ─── GetDealsStatus ─────────────────────────────────────────────────

        [Test]
        public async Task GetDealsStatus_ReturnsAllEnumValues()
        {
            // Arrange
            var expectedStatuses = Enum.GetNames(typeof(DealsStatusEnum)).ToList();

            // Act
            var result = await _dealServicesMock.GetDealsStatus();

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();

            var items = result.Data!;

            await Assert.That(items).Count().IsEqualTo(expectedStatuses.Count);

            foreach (var status in expectedStatuses)
            {
                await Assert.That(items.Contains(status)).IsTrue();
            }
        }

        // ─── GetComapanyDealsAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetComapanyDealsAsync_FiltersByCompanyAndMapsPropertiesCorrectly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var salesmanId = Guid.NewGuid();

            var salesman = new ApplicationUser
            {
                Id = salesmanId,
                UserName = $"User_{uniqueSuffix}",
                NormalizedUserName = $"USER_{uniqueSuffix}",
                Email = $"u_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"U_{uniqueSuffix}@T.PL",
                FirstName = "Tomasz",
                LastName = "Kowalski"
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Euro",
                Code = "EUR",
                DecimalPlaces = 2
            };

            var targetCompany = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Target_{uniqueSuffix}",
                NIP = "111",
                OwnerId = salesmanId,
                Owner = salesman
            };

            var otherCompany = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Other_{uniqueSuffix}",
                NIP = "222",
                OwnerId = salesmanId,
                Owner = salesman
            };

            var targetDeal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Projekt X",
                Value = 5005000,
                Status = DealsStatusEnum.ToDo,
                CloseDate = DateTime.UtcNow,
                CompanyId = targetCompany.Id,
                Company = targetCompany,
                OwnerId = salesmanId,
                Owner = salesman,
                CurrencyId = currency.Id,
                Currency = currency
            };

            var otherDeal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Projekt Y",
                Value = 1000000,
                Status = DealsStatusEnum.InProgress,
                CloseDate = DateTime.UtcNow,
                CompanyId = otherCompany.Id,
                Company = otherCompany,
                OwnerId = salesmanId,
                Owner = salesman,
                CurrencyId = currency.Id,
                Currency = currency
            };

            _contextMock.Users.Add(salesman);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.AddRange(targetCompany, otherCompany);
            _contextMock.Deals.AddRange(targetDeal, otherDeal);
            await _contextMock.SaveChangesAsync();

            var command = new CompanyCommand
            {
                PageNumber = 1,
                PageSize = 10,
                CompanyId = targetCompany.Id
            };

            // Act
            var result = await _dealServicesMock.GetComapanyDealsAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var items = result.Data!.Items;

            await Assert.That(items).Count().IsEqualTo(1);

            var mappedDeal = items.First();

            await Assert.That(mappedDeal.Id).IsEqualTo(targetDeal.Id);
            await Assert.That(mappedDeal.Name).IsEqualTo("Projekt X");
            await Assert.That(mappedDeal.Value).IsEqualTo(500.50m);
            await Assert.That(mappedDeal.SalesmanFirstName).IsEqualTo("Tomasz");
            await Assert.That(mappedDeal.SalesmanLastName).IsEqualTo("Kowalski");
            await Assert.That(mappedDeal.Code).IsEqualTo("EUR");
            await Assert.That(mappedDeal.DecimalPlaces).IsEqualTo(2);
        }

        [Test]
        public async Task GetComapanyDealsAsync_AppliesPaginationCorrectly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userId = Guid.NewGuid();

            var user = new ApplicationUser
            {
                Id = userId,
                UserName = $"U_{uniqueSuffix}",
                NormalizedUserName = $"U_{uniqueSuffix}",
                Email = $"e_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"E_{uniqueSuffix}@T.PL",
                FirstName = "Tomasz",
                LastName = "Kowalski"
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "PLN",
                Code = "PLN",
                DecimalPlaces = 2
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Comp_{uniqueSuffix}",
                NIP = "123",
                OwnerId = userId,
                Owner = user
            };

            var deals = new List<Deal>
            {
                new Deal
                {
                    Id = Guid.NewGuid(),
                    Name = "D1",
                    Value = 10000,
                    CompanyId = company.Id,
                    Company = company,
                    OwnerId = userId,
                    Owner = user,
                    CurrencyId = currency.Id,
                    Currency = currency,
                    CloseDate = DateTime.UtcNow
                },

                new Deal
                {
                    Id = Guid.NewGuid(),
                    Name = "D2",
                    Value = 20000,
                    CompanyId = company.Id,
                    Company = company,
                    OwnerId = userId,
                    Owner = user,
                    CurrencyId = currency.Id,
                    Currency = currency,
                    CloseDate = DateTime.UtcNow
                },

                new Deal
                {
                    Id = Guid.NewGuid(),
                    Name = "D3",
                    Value = 30000,
                    CompanyId = company.Id,
                    Company = company,
                    OwnerId = userId,
                    Owner = user,
                    CurrencyId = currency.Id,
                    Currency = currency,
                    CloseDate = DateTime.UtcNow
                }
            };

            _contextMock.Users.Add(user);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            _contextMock.Deals.AddRange(deals);
            await _contextMock.SaveChangesAsync();

            var command = new CompanyCommand
            {
                PageNumber = 1,
                PageSize = 2,
                CompanyId = company.Id
            };

            // Act
            var result = await _dealServicesMock.GetComapanyDealsAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();
        }

        [Test]
        public async Task GetComapanyDealsAsync_WhenNoSalesFound_ReturnsEmptyListWithSuccessStatus()
        {
            // Arrange
            var randomCompanyId = Guid.NewGuid();
            var command = new CompanyCommand { PageNumber = 1, PageSize = 10, CompanyId = randomCompanyId };

            // Act
            var result = await _dealServicesMock.GetComapanyDealsAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();

            await Assert.That(result.Data!.Items).IsEmpty();
        }

        // ─── GetDealDetailAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetSaleDetailAsync_WhenDealDoesNotExist_Returns404NotFound()
        {
            // Arrange
            var randomDealId = Guid.NewGuid();
            var randomUserId = Guid.NewGuid();

            // Act
            var result = await _dealServicesMock.GetDealDetailAsync(randomDealId, randomUserId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.Message).IsEqualTo("Deal not found.");
        }

        [Test]
        public async Task GetDealDetailAsync_WhenDealHasNoInvoices_ReturnsAggregatesAsZero()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userId = Guid.NewGuid();

            var user = new ApplicationUser
            {
                Id = userId,
                UserName = $"U_{uniqueSuffix}",
                NormalizedUserName = $"U_{uniqueSuffix}",
                Email = $"e_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"E_{uniqueSuffix}@T.PL",
                FirstName = "Tomasz",
                LastName = "Kowalski"
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "PLN",
                Code = "PLN",
                DecimalPlaces = 2,
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Comp_{uniqueSuffix}",
                NIP = "111",
                OwnerId = userId,
                Owner = user
            };

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "New Project",
                Value = 1000000,
                Status = DealsStatusEnum.Complete,
                CloseDate = DateTime.UtcNow,
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = user,
                CurrencyId = currency.Id,
                Currency = currency
            };

            _contextMock.Users.Add(user);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            _contextMock.Deals.Add(deal);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _dealServicesMock.GetDealDetailAsync(deal.Id, userId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var data = result.Data!;

            await Assert.That(data.Value).IsEqualTo(1000000);
            await Assert.That(data.InvoicedAmount).IsEqualTo(0);
            await Assert.That(data.PaidAmount).IsEqualTo(0);
            await Assert.That(data.IsOverduelInvoices).IsFalse();
            await Assert.That(data.PaymentPercentage).IsEqualTo(0);
        }

        [Test]
        public async Task GetDealDetailAsync_CalculatesAggregatesAndOverdueStatusCorrectly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userId = Guid.NewGuid();

            var user = new ApplicationUser
            {
                Id = userId,
                UserName = $"U2_{uniqueSuffix}",
                NormalizedUserName = $"U2_{uniqueSuffix}",
                Email = $"e2_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"E2_{uniqueSuffix}@T.PL",
                FirstName = "Tomasz",
                LastName = "Kowalski"
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Euro",
                Code = "EUR",
                DecimalPlaces = 2
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Comp2_{uniqueSuffix}",
                NIP = "222",
                OwnerId = userId,
                Owner = user
            };

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Complex Project",
                Value = 1000000,
                Status = DealsStatusEnum.Complete,
                CloseDate = DateTime.UtcNow,
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = user,
                CurrencyId = currency.Id,
                Currency = currency
            };

            var invoice1 = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = "F/1",
                CompanyId = company.Id,
                Company = company,
                CurrencyId = currency.Id,
                Currency = currency,
                TotalAmount = 400000,
                PaidAmount = 400000,
                DueDate = DateTime.UtcNow.AddDays(-10)
            };

            var invoice2 = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = "F/2",
                CompanyId = company.Id,
                Company = company,
                CurrencyId = currency.Id,
                Currency = currency,
                TotalAmount = 600000,
                PaidAmount = 150000,
                DueDate = DateTime.UtcNow.AddDays(-1)
            };

            deal.Invoices = new List<Invoice> { invoice1, invoice2 };

            _contextMock.Users.Add(user);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            _contextMock.Deals.Add(deal);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _dealServicesMock.GetDealDetailAsync(deal.Id, userId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var data = result.Data!;

            await Assert.That(data.CompanyName).IsEqualTo(company.Name);
            await Assert.That(data.CurrencyCode).IsEqualTo("EUR");
            await Assert.That(data.InvoicedAmount).IsEqualTo(1000000);
            await Assert.That(data.PaidAmount).IsEqualTo(550000);
            await Assert.That(data.IsOverduelInvoices).IsTrue();
            await Assert.That(data.PaymentPercentage).IsEqualTo(55);
        }

        [Test]
        public async Task GetDealDetailAsync_WhenDealHasMissingCompanyRelation_ThrowsDataCorruptionException()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userId = Guid.NewGuid();

            var user = new ApplicationUser
            {
                Id = userId,
                UserName = $"U_{uniqueSuffix}",
                NormalizedUserName = $"U_{uniqueSuffix}",
                Email = $"e_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"E_{uniqueSuffix}@T.PL",
                FirstName = "Adam",
                LastName = "Kowalski"
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "PLN",
                Code = "PLN",
                DecimalPlaces = 2
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Firma_{uniqueSuffix}",
                NIP = "1234567890",
                OwnerId = userId,
                Owner = user
            };

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Projekt ze skażoną firmą",
                Value = 50000,
                Status = DealsStatusEnum.ToDo,
                CloseDate = DateTime.UtcNow,
                CompanyId = company.Id,
                OwnerId = userId,
                CurrencyId = currency.Id
            };

            _contextMock.Users.Add(user);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            _contextMock.Deals.Add(deal);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            await _contextMock.Database.ExecuteSqlRawAsync(@"
                SET session_replication_role = 'replica';
                DELETE FROM ""Companies"";
                SET session_replication_role = 'origin';
            ");

            // Act & Assert
            await Assert.That(async () => await _dealServicesMock.GetDealDetailAsync(deal.Id, userId))
                .Throws<DataCorruptionException>();
        }

        [Test]
        public async Task GetDealDetailAsync_WhenDealHasMissingCurrencyRelation_ThrowsDataCorruptionException()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userId = Guid.NewGuid();

            var user = new ApplicationUser
            {
                Id = userId,
                UserName = $"U_{uniqueSuffix}",
                NormalizedUserName = $"U_{uniqueSuffix}",
                Email = $"e_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"E_{uniqueSuffix}@T.PL",
                FirstName = "Piotr",
                LastName = "Nowak"
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "EUR",
                Code = "EUR",
                DecimalPlaces = 2
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Firma2_{uniqueSuffix}",
                NIP = "9876543210",
                OwnerId = userId,
                Owner = user
            };

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Projekt ze skażoną walutą",
                Value = 100000,
                Status = DealsStatusEnum.InProgress,
                CloseDate = DateTime.UtcNow,
                CompanyId = company.Id,
                OwnerId = userId,
                CurrencyId = currency.Id
            };

            _contextMock.Users.Add(user);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            _contextMock.Deals.Add(deal);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            await _contextMock.Database.ExecuteSqlRawAsync(@"
                SET session_replication_role = 'replica';
                DELETE FROM ""Currencies"";
                SET session_replication_role = 'origin';
            ");

            // Act & Assert
            await Assert.That(async () => await _dealServicesMock.GetDealDetailAsync(deal.Id, userId))
                .Throws<DataCorruptionException>();
        }

        [Test]
        public async Task GetDealDetailAsync_WhenDealHasCorruptedNegativeValue_ThrowsDataCorruptionException()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userId = Guid.NewGuid();

            var user = new ApplicationUser
            {
                Id = userId,
                UserName = $"U_{uniqueSuffix}",
                NormalizedUserName = $"U_{uniqueSuffix}",
                Email = $"e_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"E_{uniqueSuffix}@T.PL",
                FirstName = "Jan",
                LastName = "Kowalski"
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "PLN",
                Code = "PLN",
                DecimalPlaces = 2
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Firma3_{uniqueSuffix}",
                NIP = "5556667778",
                OwnerId = userId,
                Owner = user
            };

            var corruptedDeal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Skażona ujemna wartość",
                Value = -50000,
                Status = DealsStatusEnum.InProgress,
                CloseDate = DateTime.UtcNow,
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = user,
                CurrencyId = currency.Id,
                Currency = currency
            };

            _contextMock.Users.Add(user);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            _contextMock.Deals.Add(corruptedDeal);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            // Act & Assert
            await Assert.That(async () => await _dealServicesMock.GetDealDetailAsync(corruptedDeal.Id, userId))
                .Throws<DataCorruptionException>();
        }

        [Test]
        public async Task GetDealDetailAsync_WhenInvoiceHasCorruptedNegativePaidAmount_ThrowsDataCorruptionException()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userId = Guid.NewGuid();

            var user = new ApplicationUser
            {
                Id = userId,
                UserName = $"U_{uniqueSuffix}",
                NormalizedUserName = $"U_{uniqueSuffix}",
                Email = $"e_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"E_{uniqueSuffix}@T.PL",
                FirstName = "Tomasz",
                LastName = "Kowalski"
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "PLN",
                Code = "PLN",
                DecimalPlaces = 2
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Firma4_{uniqueSuffix}",
                NIP = "1114447770",
                OwnerId = userId,
                Owner = user
            };

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Projekt z wadliwą fakturą",
                Value = 100000,
                Status = DealsStatusEnum.Complete,
                CloseDate = DateTime.UtcNow,
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = user,
                CurrencyId = currency.Id,
                Currency = currency
            };

            var corruptedInvoice = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = "FV/CORRUPTED/01",
                TotalAmount = 50000,
                PaidAmount = -20000,
                CompanyId = company.Id,
                Company = company,
                CurrencyId = currency.Id,
                Currency = currency,
                DealId = deal.Id,
                IssueDate = DateTime.UtcNow.AddDays(-5),
                DueDate = DateTime.UtcNow.AddDays(10)
            };

            deal.Invoices.Add(corruptedInvoice);

            _contextMock.Users.Add(user);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            _contextMock.Deals.Add(deal);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            // Act & Assert
            await Assert.That(async () => await _dealServicesMock.GetDealDetailAsync(deal.Id, userId))
                .Throws<DataCorruptionException>();
        }

        [Test]
        public async Task GetDealDetailAsync_WhenUserIsNotOwnerNorManager_ThrowsForbiddenException()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();
            var unauthorizedUserId = Guid.NewGuid();

            var ownerUser = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"Owner_{uniqueSuffix}",
                NormalizedUserName = $"OWNER_{uniqueSuffix}",
                Email = $"owner_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"OWNER_{uniqueSuffix}@T.PL",
                FirstName = "Owner",
                LastName = "User"
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "PLN",
                Code = "PLN",
                DecimalPlaces = 2
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Company_{uniqueSuffix}",
                NIP = "9998887766",
                OwnerId = ownerId,
                Owner = ownerUser
            };

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Prywatna szansa sprzedaży",
                Value = 200000,
                Status = DealsStatusEnum.InProgress,
                CloseDate = DateTime.UtcNow,
                CompanyId = company.Id,
                Company = company,
                OwnerId = ownerId,
                Owner = ownerUser,
                CurrencyId = currency.Id,
                Currency = currency
            };

            _contextMock.Users.Add(ownerUser);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            _contextMock.Deals.Add(deal);
            await _contextMock.SaveChangesAsync();

            // Act & Assert
            await Assert.That(async () => await _dealServicesMock.GetDealDetailAsync(deal.Id, unauthorizedUserId))
                .Throws<ForbiddenException>();
        }

        // ─── GetDealProductAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetDealProductAsync_MapsDeepRelationsAndCalculatesTotalsCorrectly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userId = Guid.NewGuid();

            var user = new ApplicationUser
            {
                Id = userId,
                UserName = $"U_{uniqueSuffix}",
                NormalizedUserName = $"U_{uniqueSuffix}",
                Email = $"e_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"E_{uniqueSuffix}@T.PL",
                FirstName = "Tomasz",
                LastName = "Kowalski"
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Złoty",
                Code = "PLN",
                DecimalPlaces = 2
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Comp_{uniqueSuffix}",
                NIP = "111",
                OwnerId = userId,
                Owner = user
            };

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Deal",
                Value = 0,
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = user,
                CurrencyId = currency.Id,
                Currency = currency,
                CloseDate = DateTime.UtcNow
            };

            var unit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = "Sztuka",
                Symbol = "szt"
            };

            var steelGrade = new SteelGrade
            {
                Id = Guid.NewGuid(),
                Name = "S235"
            };

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Produkt A",
                SteelGradeId = steelGrade.Id,
                SteelGrade = steelGrade,
                Thickness = 2,
                Width = 40,
                Length = 6000,
                PricePerUnit = 10000,
                UnitId = unit.Id,
                Unit = unit,
                Category = ProductCategoryEnum.Other,
                CurrencyId = currency.Id,
                Currency = currency
            };

            var dealProduct = new DealProduct
            {
                Id = Guid.NewGuid(),
                DealId = deal.Id,
                Deal = deal,
                ProductId = product.Id,
                Product = product,
                Quantity = 5,
                UnitPrice = 12000
            };

            _contextMock.SteelGrades.Add(steelGrade);
            _contextMock.Users.Add(user);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            _contextMock.Deals.Add(deal);
            _contextMock.UnitsOfMeasure.Add(unit);
            _contextMock.Products.Add(product);
            _contextMock.DealProducts.Add(dealProduct);
            await _contextMock.SaveChangesAsync();

            var command = new ProductListCommand { PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _dealServicesMock.GetDealProductAsync(deal.Id, command, userId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var items = result.Data!.Items;
            await Assert.That(items).Count().IsEqualTo(1);

            var mapped = items.First();

            await Assert.That(mapped.Name).IsEqualTo("Produkt A");
            await Assert.That(mapped.SteelGrade).IsEqualTo("S235");
            await Assert.That(mapped.UnitSymbol).IsEqualTo("szt");
            await Assert.That(mapped.CurrencyCode).IsEqualTo("PLN");
            await Assert.That(mapped.DecimalPlaces).IsEqualTo(2);
            await Assert.That(mapped.Quantity).IsEqualTo(5);
            await Assert.That(mapped.BaseUnitPrice).IsEqualTo(10000);
            await Assert.That(mapped.UnitPrice).IsEqualTo(12000);
            await Assert.That(mapped.TotalPrice).IsEqualTo(60000);
            await Assert.That(mapped.Dimensions).IsNotNull();
            await Assert.That(mapped.Dimensions).IsNotEmpty();
        }

        [Test]
        public async Task GetDealProductAsync_ReturnsProductsOnlyForSpecificDeal()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userId = Guid.NewGuid();

            var user = new ApplicationUser
            {
                Id = userId,
                UserName = $"U2_{uniqueSuffix}",
                NormalizedUserName = $"U2_{uniqueSuffix}",
                Email = $"e2_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"E2_{uniqueSuffix}@T.PL",
                FirstName = "Tomasz",
                LastName = "Kowalski"
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Euro",
                Code = "EUR",
                DecimalPlaces = 2
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"C2_{uniqueSuffix}",
                NIP = "222",
                OwnerId = userId,
                Owner = user
            };

            var targetDeal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Target",
                Value = 0,
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = user,
                CurrencyId = currency.Id,
                Currency = currency,
                CloseDate = DateTime.UtcNow
            };

            var otherDeal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Other",
                Value = 0,
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = user,
                CurrencyId = currency.Id,
                Currency = currency,
                CloseDate = DateTime.UtcNow
            };

            var unit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = "Metr",
                Symbol = "m"
            };

            var steelGrade = new SteelGrade
            {
                Id = Guid.NewGuid(),
                Name = "S235"
            };

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Prod B",
                SteelGradeId = steelGrade.Id,
                SteelGrade = steelGrade,
                UnitId = unit.Id,
                Unit = unit,
                Category = ProductCategoryEnum.Other,
                CurrencyId = currency.Id,
                Currency = currency
            };

            var targetDealProduct = new DealProduct
            {
                Id = Guid.NewGuid(),
                DealId = targetDeal.Id,
                Deal = targetDeal,
                ProductId = product.Id,
                Product = product,
                Quantity = 1
            };

            var otherDealProduct = new DealProduct
            {
                Id = Guid.NewGuid(),
                DealId = otherDeal.Id,
                Deal = otherDeal,
                ProductId = product.Id,
                Product = product,
                Quantity = 2
            };

            _contextMock.SteelGrades.Add(steelGrade);
            _contextMock.Users.Add(user);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            _contextMock.Deals.AddRange(targetDeal, otherDeal);
            _contextMock.UnitsOfMeasure.Add(unit);
            _contextMock.Products.Add(product);
            _contextMock.DealProducts.AddRange(targetDealProduct, otherDealProduct);
            await _contextMock.SaveChangesAsync();

            var command = new ProductListCommand
            {
                PageNumber = 1,
                PageSize = 10
            };

            // Act 
            var result = await _dealServicesMock.GetDealProductAsync(targetDeal.Id, command, userId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var items = result.Data!.Items;

            await Assert.That(items).Count().IsEqualTo(1);
            await Assert.That(items.First().Quantity).IsEqualTo(1);
        }

        [Test]
        public async Task GetDealProductAsync_WhenDealHasNoProducts_ReturnsEmptyListWithSuccessStatus()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userId = Guid.NewGuid();

            var user = new ApplicationUser
            {
                Id = userId,
                UserName = $"UserEmpty_{uniqueSuffix}",
                NormalizedUserName = $"USEREMPTY_{uniqueSuffix}",
                Email = $"ue_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"UE_{uniqueSuffix}@T.PL",
                FirstName = "Jan",
                LastName = "Nowak"
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "PLN",
                Code = "PLN",
                DecimalPlaces = 2
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"CompEmpty_{uniqueSuffix}",
                NIP = "1231231234",
                OwnerId = userId,
                Owner = user
            };

            var dealWithoutProducts = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Pusta Szansa",
                Value = 0,
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = user,
                CurrencyId = currency.Id,
                Currency = currency,
                CloseDate = DateTime.UtcNow
            };

            _contextMock.Users.Add(user);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            _contextMock.Deals.Add(dealWithoutProducts);
            await _contextMock.SaveChangesAsync();

            var command = new ProductListCommand { PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _dealServicesMock.GetDealProductAsync(dealWithoutProducts.Id, command, userId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.Items).IsEmpty();
        }

        [Test]
        public async Task GetDealProductAsync_WhenUserIsNotOwnerNorManager_ThrowsForbiddenException()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();
            var unauthorizedUserId = Guid.NewGuid();

            var ownerUser = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"OwnerP_{uniqueSuffix}",
                NormalizedUserName = $"OWNERP_{uniqueSuffix}",
                Email = $"ownerp_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"OWNERP_{uniqueSuffix}@T.PL",
                FirstName = "Owner",
                LastName = "User"
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "PLN",
                Code = "PLN",
                DecimalPlaces = 2
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"CompP_{uniqueSuffix}",
                NIP = "9990001122",
                OwnerId = ownerId,
                Owner = ownerUser
            };

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Projekt z produktami",
                Value = 50000,
                CompanyId = company.Id,
                Company = company,
                OwnerId = ownerId,
                Owner = ownerUser,
                CurrencyId = currency.Id,
                Currency = currency,
                CloseDate = DateTime.UtcNow
            };

            _contextMock.Users.Add(ownerUser);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            _contextMock.Deals.Add(deal);
            await _contextMock.SaveChangesAsync();

            var command = new ProductListCommand { PageNumber = 1, PageSize = 10 };

            // Act & Assert
            await Assert.That(async () => await _dealServicesMock.GetDealProductAsync(deal.Id, command, unauthorizedUserId))
                .Throws<ForbiddenException>();
        }

        // ─── AddDealAsync ─────────────────────────────────────────────────

        [Test]
        public async Task AddDealAsync_ThrowsUserNotFoundException_WhenUserDoesNotExist()
        {
            // Arrange
            var nonExistentUserId = Guid.NewGuid();
            var command = new AddDealCommand
            {
                CloseDate = DateTime.UtcNow.AddDays(14),
                CompanyId = Guid.NewGuid(),
                CurrencyId = Guid.NewGuid(),
                Products = new List<AddDealProductCommand>()
            };

            // Act & Assert
            await Assert.That(async () => await _dealServicesMock.AddDealAsync(command, nonExistentUserId))
                .Throws<UserNotFoundException>();
        }

        [Test]
        public async Task AddDealAsync_ReturnsNotFound_WhenCompanyDoesNotExist()
        {
            // Arrange
            var (company, user, currency) = await SeedCompanyAndUserAsync();
            var nonExistentCompanyId = Guid.NewGuid();

            var command = new AddDealCommand
            {
                CloseDate = DateTime.UtcNow.AddDays(14),
                CompanyId = nonExistentCompanyId,
                CurrencyId = currency.Id,
                Products = new List<AddDealProductCommand>
                {
                    new() { ProductId = Guid.NewGuid(), Quantity = 1, UnitPrice = 1000 }
                }
            };

            // Act
            var result = await _dealServicesMock.AddDealAsync(command, user.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.CompanyNotFound);
        }

        [Test]
        public async Task AddDealAsync_ReturnsNotFound_WhenCurrencyDoesNotExist()
        {
            // Arrange
            var (company, user, _) = await SeedCompanyAndUserAsync();
            var nonExistentCurrencyId = Guid.NewGuid();

            var command = new AddDealCommand
            {
                CloseDate = DateTime.UtcNow.AddDays(14),
                CompanyId = company.Id,
                CurrencyId = nonExistentCurrencyId,
                Products = new List<AddDealProductCommand>
                {
                    new() { ProductId = Guid.NewGuid(), Quantity = 1, UnitPrice = 1000 }
                }
            };

            // Act
            var result = await _dealServicesMock.AddDealAsync(command, user.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.CurrencyNotFound);
        }

        [Test]
        public async Task AddDealAsync_ReturnsBadRequest_WhenProductsListIsEmpty()
        {
            // Arrange
            var (company, user, currency) = await SeedCompanyAndUserAsync();

            var command = new AddDealCommand
            {
                CloseDate = DateTime.UtcNow.AddDays(14),
                CompanyId = company.Id,
                CurrencyId = currency.Id,
                Products = new List<AddDealProductCommand>()
            };

            // Act
            var result = await _dealServicesMock.AddDealAsync(command, user.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);
        }

        [Test]
        [Arguments(0, 1000)]
        [Arguments(-5, 1000)]
        [Arguments(1, -50)]
        public async Task AddDealAsync_ReturnsBadRequest_WhenProductsContainInvalidQuantityOrPrice(int quantity, long unitPrice)
        {
            // Arrange
            var (company, user, currency) = await SeedCompanyAndUserAsync();

            var command = new AddDealCommand
            {
                CloseDate = DateTime.UtcNow.AddDays(14),
                CompanyId = company.Id,
                CurrencyId = currency.Id,
                Products = new List<AddDealProductCommand>
                {
                    new() { ProductId = Guid.NewGuid(), Quantity = quantity, UnitPrice = unitPrice }
                }
            };

            // Act
            var result = await _dealServicesMock.AddDealAsync(command, user.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);
        }

        [Test]
        public async Task AddDealAsync_ReturnsNotFound_WhenSpecifiedProductDoesNotExistInDatabase()
        {
            // Arrange
            var (company, user, currency) = await SeedCompanyAndUserAsync();
            var nonExistentProductId = Guid.NewGuid();

            var command = new AddDealCommand
            {
                CloseDate = DateTime.UtcNow.AddDays(14),
                CompanyId = company.Id,
                CurrencyId = currency.Id,
                Products = new List<AddDealProductCommand>
                {
                    new() { ProductId = nonExistentProductId, Quantity = 2, UnitPrice = 5000 }
                }
            };

            // Act
            var result = await _dealServicesMock.AddDealAsync(command, user.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.ProductNotFound);
            await Assert.That(result.Errors).IsNotNull();
            await Assert.That(result.Errors!.Count).IsEqualTo(1);
        }

        [Test]
        public async Task AddDealAsync_CreatesDealAndDealProducts_WhenDataIsValid()
        {
            // Arrange
            var (company, user, currency) = await SeedCompanyAndUserAsync();

            var steelGrade = new SteelGrade { Id = Guid.NewGuid(), Name = "1.4301", Density = 7900, IsDeleted = false };
            var unit = new UnitOfMeasure { Id = Guid.NewGuid(), Name = "Sztuka", Symbol = "szt.", BaseMultiplier = 1, IsDeleted = false };
            _contextMock.SteelGrades.Add(steelGrade);
            _contextMock.UnitsOfMeasure.Add(unit);

            var productA = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Produkt A",
                SteelGradeId = steelGrade.Id,
                UnitId = unit.Id,
                CurrencyId = currency.Id,
                PricePerUnit = 20000,
                StockQuantity = 50,
                Category = ProductCategoryEnum.Pipe,
                SteelGrade = steelGrade,
                Unit = unit
            };
            var productB = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Produkt B",
                SteelGradeId = steelGrade.Id,
                UnitId = unit.Id,
                CurrencyId = currency.Id,
                PricePerUnit = 30000,
                StockQuantity = 50,
                Category = ProductCategoryEnum.Sheet,
                SteelGrade = steelGrade,
                Unit = unit
            };
            _contextMock.Products.AddRange(productA, productB);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            var closeDate = DateTime.UtcNow.AddDays(30);
            var command = new AddDealCommand
            {
                CloseDate = closeDate,
                CompanyId = company.Id,
                CurrencyId = currency.Id,
                Products = new List<AddDealProductCommand>
                {
                    new() { ProductId = productA.Id, Quantity = 2, UnitPrice = 18000 },
                    new() { ProductId = productB.Id, Quantity = 4, UnitPrice = 25000 }
                }
            };

            var expectedTotalValue = (2L * 18000) + (4L * 25000);

            // Act
            var result = await _dealServicesMock.AddDealAsync(command, user.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status201Created);

            var createdDeal = await _contextMock.Deals
            .Include(d => d.DealProducts)
            .FirstOrDefaultAsync(d => d.CompanyId == company.Id && d.OwnerId == user.Id);

            await Assert.That(createdDeal).IsNotNull();
            await Assert.That(createdDeal!.Name.StartsWith("D/")).IsTrue();
            await Assert.That(createdDeal!.OwnerId).IsEqualTo(user.Id);
            await Assert.That(createdDeal.CompanyId).IsEqualTo(company.Id);
            await Assert.That(createdDeal.CurrencyId).IsEqualTo(currency.Id);
            await Assert.That(createdDeal.Status).IsEqualTo(DealsStatusEnum.ToDo);
            await Assert.That(createdDeal.Value).IsEqualTo(expectedTotalValue);
            await Assert.That(createdDeal.DealProducts.Count).IsEqualTo(2);

            var productAEntry = createdDeal.DealProducts.FirstOrDefault(dp => dp.ProductId == productA.Id);
            await Assert.That(productAEntry).IsNotNull();
            await Assert.That(productAEntry!.Quantity).IsEqualTo(2);
            await Assert.That(productAEntry.UnitPrice).IsEqualTo(18000);
        }

        // ─── DeleteDealAsync ─────────────────────────────────────────────────

        [Test]
        public async Task DeleteDealAsync_WhenDealDoesNotExist_Returns404NotFound()
        {
            // Arrange
            var randomUserId = Guid.NewGuid();
            var randomDealId = Guid.NewGuid();

            // Act
            var result = await _dealServicesMock.DeleteDealAsync(randomUserId, randomDealId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.DealNotFound);
        }

        [Test]
        public async Task DeleteDealAsync_WhenUserIsNotOwnerNorManager_ThrowsForbiddenException()
        {
            // Arrange
            var (company, owner, currency) = await SeedCompanyAndUserAsync();
            var unauthorizedUserId = Guid.NewGuid();

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "D/2026/09/11/0001",
                Value = 50000,
                Status = DealsStatusEnum.ToDo,
                CloseDate = DateTime.UtcNow.AddDays(7),
                CompanyId = company.Id,
                OwnerId = owner.Id,
                CurrencyId = currency.Id
            };

            _contextMock.Deals.Add(deal);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            // Act & Assert
            await Assert.That(async () => await _dealServicesMock.DeleteDealAsync(unauthorizedUserId, deal.Id))
                .Throws<ForbiddenException>();
        }

        [Test]
        [Arguments(DealsStatusEnum.ToDo)]
        [Arguments(DealsStatusEnum.InProgress)]
        public async Task DeleteDealAsync_WhenDealIsInValidStatus_TransitionsToCancelledAndSaves(DealsStatusEnum initialStatus)
        {
            // Arrange
            var (company, owner, currency) = await SeedCompanyAndUserAsync();

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "D/2026/09/11/0002",
                Value = 100000,
                Status = initialStatus,
                CloseDate = DateTime.UtcNow.AddDays(14),
                CompanyId = company.Id,
                OwnerId = owner.Id,
                CurrencyId = currency.Id
            };

            _contextMock.Deals.Add(deal);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            // Act
            var result = await _dealServicesMock.DeleteDealAsync(owner.Id, deal.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var updatedDeal = await _contextMock.Deals.AsNoTracking().FirstOrDefaultAsync(d => d.Id == deal.Id);
            await Assert.That(updatedDeal).IsNotNull();
            await Assert.That(updatedDeal!.Status).IsEqualTo(DealsStatusEnum.Cancelled);
        }

        [Test]
        public async Task DeleteDealAsync_WhenDealIsAlreadyComplete_ReturnsBadRequestAndDoesNotChangeStatus()
        {
            // Arrange
            var (company, owner, currency) = await SeedCompanyAndUserAsync();

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "D/2026/09/11/0003",
                Value = 75000,
                Status = DealsStatusEnum.Complete,
                CloseDate = DateTime.UtcNow.AddDays(-1),
                CompanyId = company.Id,
                OwnerId = owner.Id,
                CurrencyId = currency.Id
            };

            _contextMock.Deals.Add(deal);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            // Act
            var result = await _dealServicesMock.DeleteDealAsync(owner.Id, deal.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);

            var dbDeal = await _contextMock.Deals.AsNoTracking().FirstOrDefaultAsync(d => d.Id == deal.Id);
            await Assert.That(dbDeal).IsNotNull();
            await Assert.That(dbDeal!.Status).IsEqualTo(DealsStatusEnum.Complete);
        }

        [Test]
        public async Task DeleteDealAsync_WhenDealIsAlreadyCancelled_ReturnsBadRequest()
        {
            // Arrange
            var (company, owner, currency) = await SeedCompanyAndUserAsync();

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "D/2026/09/11/0004",
                Value = 25000,
                Status = DealsStatusEnum.Cancelled,
                CloseDate = DateTime.UtcNow,
                CompanyId = company.Id,
                OwnerId = owner.Id,
                CurrencyId = currency.Id
            };

            _contextMock.Deals.Add(deal);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            // Act
            var result = await _dealServicesMock.DeleteDealAsync(owner.Id, deal.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);
            await Assert.That(result.Message).IsEqualTo("Deal is already in status 'Cancelled'.");
        }

        // ─── ExtendDealCloseDateAsync ─────────────────────────────────────────────────

        [Test]
        public async Task ExtendDealCloseDateAsync_WhenDealDoesNotExist_Returns404NotFound()
        {
            // Arrange
            var randomUserId = Guid.NewGuid();
            var command = new ExtendDealCloseDateCommand
            {
                DealId = Guid.NewGuid(),
                NewCloseDate = DateTime.UtcNow.AddDays(30)
            };

            // Act
            var result = await _dealServicesMock.ExtendDealCloseDateAsync(command, randomUserId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.DealNotFound);
        }

        [Test]
        public async Task ExtendDealCloseDateAsync_WhenUserIsNotOwnerNorAuthorized_ThrowsForbiddenException()
        {
            // Arrange
            var (company, owner, currency) = await SeedCompanyAndUserAsync();
            var unauthorizedUserId = Guid.NewGuid();

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "D/2026/09/11/0010",
                Value = 50000,
                Status = DealsStatusEnum.ToDo,
                CloseDate = DateTime.UtcNow.AddDays(7),
                CompanyId = company.Id,
                OwnerId = owner.Id,
                CurrencyId = currency.Id
            };

            _contextMock.Deals.Add(deal);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            var command = new ExtendDealCloseDateCommand
            {
                DealId = deal.Id,
                NewCloseDate = DateTime.UtcNow.AddDays(14)
            };

            // Act & Assert
            await Assert.That(async () => await _dealServicesMock.ExtendDealCloseDateAsync(command, unauthorizedUserId))
                .Throws<ForbiddenException>();
        }

        [Test]
        [Arguments(DealsStatusEnum.Complete)]
        [Arguments(DealsStatusEnum.Cancelled)]
        public async Task ExtendDealCloseDateAsync_WhenDealIsFinalized_ReturnsBadRequest(DealsStatusEnum finalizedStatus)
        {
            // Arrange
            var (company, owner, currency) = await SeedCompanyAndUserAsync();

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "D/2026/09/11/0011",
                Value = 50000,
                Status = finalizedStatus,
                CloseDate = DateTime.UtcNow.AddDays(7),
                CompanyId = company.Id,
                OwnerId = owner.Id,
                CurrencyId = currency.Id
            };

            _contextMock.Deals.Add(deal);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            var command = new ExtendDealCloseDateCommand
            {
                DealId = deal.Id,
                NewCloseDate = DateTime.UtcNow.AddDays(14)
            };

            // Act
            var result = await _dealServicesMock.ExtendDealCloseDateAsync(command, owner.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);
            await Assert.That(result.Message).IsEqualTo("Cannot modify a finalized deal with status '" + finalizedStatus + "'.");
        }

        [Test]
        public async Task ExtendDealCloseDateAsync_WhenNewDateIsEarlierOrEqualToCurrent_ReturnsBadRequest()
        {
            // Arrange
            var (company, owner, currency) = await SeedCompanyAndUserAsync();
            var currentCloseDate = DateTime.UtcNow.AddDays(10);

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "D/2026/09/11/0012",
                Value = 50000,
                Status = DealsStatusEnum.ToDo,
                CloseDate = currentCloseDate,
                CompanyId = company.Id,
                OwnerId = owner.Id,
                CurrencyId = currency.Id
            };

            _contextMock.Deals.Add(deal);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            var command = new ExtendDealCloseDateCommand
            {
                DealId = deal.Id,
                NewCloseDate = currentCloseDate.AddDays(-2) 
            };

            // Act
            var result = await _dealServicesMock.ExtendDealCloseDateAsync(command, owner.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);
            await Assert.That(result.Message).IsEqualTo("New close date must be later than the current close date.");
        }

        [Test]
        [Arguments(DealsStatusEnum.ToDo)]
        [Arguments(DealsStatusEnum.InProgress)]
        public async Task ExtendDealCloseDateAsync_WhenDataIsValid_UpdatesCloseDateAndReturnsOk(DealsStatusEnum status)
        {
            // Arrange
            var (company, owner, currency) = await SeedCompanyAndUserAsync();
            var initialDate = DateTime.UtcNow.AddDays(5);
            var extendedDate = DateTime.UtcNow.AddDays(20);

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "D/2026/09/11/0013",
                Value = 50000,
                Status = status,
                CloseDate = initialDate,
                CompanyId = company.Id,
                OwnerId = owner.Id,
                CurrencyId = currency.Id
            };

            _contextMock.Deals.Add(deal);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            var command = new ExtendDealCloseDateCommand
            {
                DealId = deal.Id,
                NewCloseDate = extendedDate
            };

            // Act
            var result = await _dealServicesMock.ExtendDealCloseDateAsync(command, owner.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var updatedDeal = await _contextMock.Deals.AsNoTracking().FirstOrDefaultAsync(d => d.Id == deal.Id);
            await Assert.That(updatedDeal).IsNotNull();

            var difference = (updatedDeal!.CloseDate - extendedDate).Duration();
            await Assert.That(difference < TimeSpan.FromSeconds(1)).IsTrue();
        }

        // ─── AddDealProductAsync ─────────────────────────────────────────────────

        [Test]
        public async Task AddDealProductAsync_WhenDealDoesNotExist_Returns404NotFound()
        {
            // Arrange
            var randomUserId = Guid.NewGuid();
            var randomDealId = Guid.NewGuid();
            var command = new AddDealProductCommand
            {
                ProductId = Guid.NewGuid(),
                Quantity = 2,
                UnitPrice = 15000
            };

            // Act
            var result = await _dealServicesMock.AddDealProductAsync(randomDealId, command, randomUserId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.DealNotFound);
        }

        [Test]
        public async Task AddDealProductAsync_WhenUserIsNotAuthorized_ThrowsForbiddenException()
        {
            // Arrange
            var (company, owner, currency) = await SeedCompanyAndUserAsync();
            var unauthorizedUserId = Guid.NewGuid();

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "D/2026/09/11/0020",
                Value = 0,
                Status = DealsStatusEnum.ToDo,
                CloseDate = DateTime.UtcNow.AddDays(14),
                CompanyId = company.Id,
                OwnerId = owner.Id,
                CurrencyId = currency.Id
            };

            _contextMock.Deals.Add(deal);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            var command = new AddDealProductCommand
            {
                ProductId = Guid.NewGuid(),
                Quantity = 1,
                UnitPrice = 10000
            };

            // Act & Assert
            await Assert.That(async () => await _dealServicesMock.AddDealProductAsync(deal.Id, command, unauthorizedUserId))
                .Throws<ForbiddenException>();
        }

        [Test]
        [Arguments(DealsStatusEnum.Complete)]
        [Arguments(DealsStatusEnum.Cancelled)]
        public async Task AddDealProductAsync_WhenDealIsFinalized_ReturnsBadRequest(DealsStatusEnum status)
        {
            // Arrange
            var (company, owner, currency) = await SeedCompanyAndUserAsync();

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "D/2026/09/11/0021",
                Value = 0,
                Status = status,
                CloseDate = DateTime.UtcNow.AddDays(14),
                CompanyId = company.Id,
                OwnerId = owner.Id,
                CurrencyId = currency.Id
            };

            _contextMock.Deals.Add(deal);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            var command = new AddDealProductCommand
            {
                ProductId = Guid.NewGuid(),
                Quantity = 1,
                UnitPrice = 10000
            };

            // Act
            var result = await _dealServicesMock.AddDealProductAsync(deal.Id, command, owner.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);
        }

        [Test]
        public async Task AddDealProductAsync_WhenProductAlreadyExistsInDeal_ReturnsBadRequest()
        {
            // Arrange
            var (company, owner, currency) = await SeedCompanyAndUserAsync();

            var steelGrade = new SteelGrade { Id = Guid.NewGuid(), Name = "S355", Density = 7850, IsDeleted = false };
            var unit = new UnitOfMeasure { Id = Guid.NewGuid(), Name = "Sztuka", Symbol = "szt.", BaseMultiplier = 1, IsDeleted = false };
            _contextMock.SteelGrades.Add(steelGrade);
            _contextMock.UnitsOfMeasure.Add(unit);

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Profil Zamknięty",
                SteelGradeId = steelGrade.Id,
                UnitId = unit.Id,
                CurrencyId = currency.Id,
                PricePerUnit = 10000,
                StockQuantity = 20,
                Category = ProductCategoryEnum.Pipe,
                SteelGrade = steelGrade,
                Unit = unit
            };
            _contextMock.Products.Add(product);

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "D/2026/09/11/0022",
                Value = 10000,
                Status = DealsStatusEnum.ToDo,
                CloseDate = DateTime.UtcNow.AddDays(14),
                CompanyId = company.Id,
                OwnerId = owner.Id,
                CurrencyId = currency.Id,
                DealProducts = new List<DealProduct>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        ProductId = product.Id,
                        Quantity = 1,
                        UnitPrice = 10000
                    }
                }
            };

            _contextMock.Deals.Add(deal);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            var command = new AddDealProductCommand
            {
                ProductId = product.Id,
                Quantity = 3,
                UnitPrice = 12000
            };

            // Act
            var result = await _dealServicesMock.AddDealProductAsync(deal.Id, command, owner.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);
            await Assert.That(result.Message).IsEqualTo("Product already exists in the deal.");
        }

        [Test]
        public async Task AddDealProductAsync_WhenProductDoesNotExistInDatabase_ReturnsNotFound()
        {
            // Arrange
            var (company, owner, currency) = await SeedCompanyAndUserAsync();
            var nonExistentProductId = Guid.NewGuid();

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "D/2026/09/11/0023",
                Value = 0,
                Status = DealsStatusEnum.ToDo,
                CloseDate = DateTime.UtcNow.AddDays(14),
                CompanyId = company.Id,
                OwnerId = owner.Id,
                CurrencyId = currency.Id
            };

            _contextMock.Deals.Add(deal);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            var command = new AddDealProductCommand
            {
                ProductId = nonExistentProductId,
                Quantity = 2,
                UnitPrice = 15000
            };

            // Act
            var result = await _dealServicesMock.AddDealProductAsync(deal.Id, command, owner.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.ProductNotFound);
        }

        [Test]
        [Arguments(DealsStatusEnum.ToDo)]
        [Arguments(DealsStatusEnum.InProgress)]
        public async Task AddDealProductAsync_WhenValid_AddsProductAndRecalculatesDealValue(DealsStatusEnum status)
        {
            // Arrange
            var (company, owner, currency) = await SeedCompanyAndUserAsync();

            var steelGrade = new SteelGrade { Id = Guid.NewGuid(), Name = "1.4301", Density = 7900, IsDeleted = false };
            var unit = new UnitOfMeasure { Id = Guid.NewGuid(), Name = "Sztuka", Symbol = "szt.", BaseMultiplier = 1, IsDeleted = false };
            _contextMock.SteelGrades.Add(steelGrade);
            _contextMock.UnitsOfMeasure.Add(unit);

            var existingProduct = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Produkt Istniejący",
                SteelGradeId = steelGrade.Id,
                UnitId = unit.Id,
                CurrencyId = currency.Id,
                PricePerUnit = 20000,
                StockQuantity = 50,
                Category = ProductCategoryEnum.Sheet,
                SteelGrade = steelGrade,
                Unit = unit
            };

            var newProduct = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Produkt Nowy",
                SteelGradeId = steelGrade.Id,
                UnitId = unit.Id,
                CurrencyId = currency.Id,
                PricePerUnit = 35000,
                StockQuantity = 30,
                Category = ProductCategoryEnum.Pipe,
                SteelGrade = steelGrade,
                Unit = unit
            };

            _contextMock.Products.AddRange(existingProduct, newProduct);

            var initialQuantity = 2;
            var initialUnitPrice = 20000L;
            var initialValue = initialQuantity * initialUnitPrice;

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "D/2026/09/11/0024",
                Value = initialValue,
                Status = status,
                CloseDate = DateTime.UtcNow.AddDays(20),
                CompanyId = company.Id,
                OwnerId = owner.Id,
                CurrencyId = currency.Id,
                DealProducts = new List<DealProduct>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        ProductId = existingProduct.Id,
                        Quantity = initialQuantity,
                        UnitPrice = initialUnitPrice
                    }
                }
            };

            _contextMock.Deals.Add(deal);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            var addedQuantity = 3;
            var addedUnitPrice = 30000L;
            var expectedNewTotalValue = initialValue + (addedQuantity * addedUnitPrice); // 40 000 + 90 000 = 130 000

            var command = new AddDealProductCommand
            {
                ProductId = newProduct.Id,
                Quantity = addedQuantity,
                UnitPrice = addedUnitPrice
            };

            // Act
            var result = await _dealServicesMock.AddDealProductAsync(deal.Id, command, owner.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var dbDeal = await _contextMock.Deals
                .Include(d => d.DealProducts)
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == deal.Id);

            await Assert.That(dbDeal).IsNotNull();
            await Assert.That(dbDeal!.Value).IsEqualTo(expectedNewTotalValue);
            await Assert.That(dbDeal.DealProducts.Count).IsEqualTo(2);

            var addedItem = dbDeal.DealProducts.FirstOrDefault(dp => dp.ProductId == newProduct.Id);
            await Assert.That(addedItem).IsNotNull();
            await Assert.That(addedItem!.Quantity).IsEqualTo(addedQuantity);
            await Assert.That(addedItem.UnitPrice).IsEqualTo(addedUnitPrice);
        }
    }
}
