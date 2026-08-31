using Domain.Enum;
using Domain.Models;
using Infrastructure;
using Infrastructure.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Services.Command.Offer;
using Services.Services;
using Testcontainers.PostgreSql;

namespace Tests.Services
{
    public class OfferServiceTest
    {
        protected AppDbContext _contextMock = null!;
        protected OfferServices _offerServicesMock = null!;
        protected ILogger<OfferServices> _loggerMock = null!;
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

            _loggerMock = new LoggerFactory().CreateLogger<OfferServices>();

            _offerServicesMock = new OfferServices(_contextMock, _loggerMock);
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

        private async Task<(Company Company, Contact Contact)> SeedCompanyAndContactAsync(
            string companyName = "Stal-Met",
            string firstName = "Jan",
            string lastName = "Kowalski")
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "user_" + Guid.NewGuid().ToString("N"),
                Email = "user_" + Guid.NewGuid().ToString("N") + "@test.pl",
                FirstName = "Test",
                LastName = "User"
            };
            _contextMock.Users.Add(user);

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = companyName,
                NIP = "1234567890",
                OwnerId = user.Id
            };
            _contextMock.Companies.Add(company);

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = firstName,
                LastName = lastName,
                CompanyId = company.Id,
                OwnerId = user.Id,
                IsPrimary = true,
                Owner = user
            };
            _contextMock.Contacts.Add(contact);

            await _contextMock.SaveChangesAsync();
            return (company, contact);
        }

        // ─── GetOfferListAsync ─────────────────────────────────────────────────


        [Test]
        public async Task GetOfferListAsync_ReturnsAllOffersPaged_WhenNoFiltersProvided()
        {
            // Arrange
            var (_, contact) = await SeedCompanyAndContactAsync();

            var offers = new List<Offer>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ContactId = contact.Id,
                    CreatedByUserId = contact.OwnerId,
                    ValidUntil = DateTime.UtcNow.AddDays(10),
                    Status = OfferStatusEnum.Sent
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    ContactId = contact.Id,
                    CreatedByUserId = contact.OwnerId,
                    ValidUntil = DateTime.UtcNow.AddDays(20),
                    Status = OfferStatusEnum.Accepted
                }
            };

            _contextMock.Offers.AddRange(offers);
            await _contextMock.SaveChangesAsync();

            var command = new OfferListCommand
            {
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _offerServicesMock.GetOfferListAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.Items.Count).IsEqualTo(2);
            await Assert.That(result.Data.TotalCount).IsEqualTo(2);
        }

        [Test]
        public async Task GetOfferListAsync_FiltersCorrectly_BySearchTermWithUnaccentAndCaseInsensitivity()
        {
            // Arrange
            var (_, contactMatching) = await SeedCompanyAndContactAsync("BudowaX", "Michał", "Żółciński");
            var (_, contactOther) = await SeedCompanyAndContactAsync("Huta Odra", "Adam", "Nowak");

            _contextMock.Offers.AddRange(
                new Offer
                {
                    Id = Guid.NewGuid(),
                    ContactId = contactMatching.Id,
                    CreatedByUserId = contactMatching.OwnerId,
                    ValidUntil = DateTime.UtcNow.AddDays(5),
                    Status = OfferStatusEnum.Sent
                },
                new Offer
                {
                    Id = Guid.NewGuid(),
                    ContactId = contactOther.Id,
                    CreatedByUserId = contactOther.OwnerId,
                    ValidUntil = DateTime.UtcNow.AddDays(5),
                    Status = OfferStatusEnum.Sent
                }
            );
            await _contextMock.SaveChangesAsync();

            var command = new OfferListCommand
            {
                SearchTerm = "zolcinski",
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _offerServicesMock.GetOfferListAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data!.Items.Count).IsEqualTo(1);
            await Assert.That(result.Data.Items.First().ContactLastName).IsEqualTo("Żółciński");
        }

        [Test]
        public async Task GetOfferListAsync_FiltersCorrectly_ByStatus()
        {
            // Arrange
            var (_, contact) = await SeedCompanyAndContactAsync();

            _contextMock.Offers.AddRange(
                new Offer
                {
                    Id = Guid.NewGuid(),
                    ContactId = contact.Id,
                    CreatedByUserId = contact.OwnerId,
                    ValidUntil = DateTime.UtcNow.AddDays(5),
                    Status = OfferStatusEnum.Sent
                },
                new Offer
                {
                    Id = Guid.NewGuid(),
                    ContactId = contact.Id,
                    CreatedByUserId = contact.OwnerId,
                    ValidUntil = DateTime.UtcNow.AddDays(-5),
                    Status = OfferStatusEnum.Expired
                }
            );
            await _contextMock.SaveChangesAsync();

            var command = new OfferListCommand
            {
                Status = OfferStatusEnum.Expired,
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _offerServicesMock.GetOfferListAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data!.Items.Count).IsEqualTo(1);
            await Assert.That(result.Data.Items.First().Status).IsEqualTo(OfferStatusEnum.Expired.ToString());
        }

        [Test]
        public async Task GetOfferListAsync_FiltersCorrectly_ByValidUntilDateRange()
        {
            // Arrange
            var (_, contact) = await SeedCompanyAndContactAsync();
            var baseDate = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

            _contextMock.Offers.AddRange(
                new Offer
                {
                    Id = Guid.NewGuid(),
                    ContactId = contact.Id,
                    CreatedByUserId = contact.OwnerId,
                    ValidUntil = baseDate.AddDays(-10),
                    Status = OfferStatusEnum.Sent
                },
                new Offer
                {
                    Id = Guid.NewGuid(),
                    ContactId = contact.Id,
                    CreatedByUserId = contact.OwnerId,
                    ValidUntil = baseDate,
                    Status = OfferStatusEnum.Sent
                },
                new Offer
                {
                    Id = Guid.NewGuid(),
                    ContactId = contact.Id,
                    CreatedByUserId = contact.OwnerId,
                    ValidUntil = baseDate.AddDays(10),
                    Status = OfferStatusEnum.Sent
                }
            );
            await _contextMock.SaveChangesAsync();

            var command = new OfferListCommand
            {
                ValidUntilFrom = baseDate.AddDays(-1),
                ValidUntilTo = baseDate.AddDays(1),
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _offerServicesMock.GetOfferListAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data!.Items.Count).IsEqualTo(1);
            await Assert.That(result.Data.Items.First().ValidUntil).IsEqualTo(baseDate);
        }

        [Test]
        public async Task GetOfferListAsync_SortsCorrectly_ByCompanyNameDescending()
        {
            // Arrange
            var (compA, contactA) = await SeedCompanyAndContactAsync("Alfa Stal", "Piotr", "Abacki");
            var (compZ, contactZ) = await SeedCompanyAndContactAsync("Zeta Met", "Paweł", "Zetowski");

            _contextMock.Offers.AddRange(
                new Offer
                {
                    Id = Guid.NewGuid(),
                    ContactId = contactA.Id,
                    CreatedByUserId = contactA.OwnerId,
                    ValidUntil = DateTime.UtcNow.AddDays(5),
                    Status = OfferStatusEnum.Sent
                },
                new Offer
                {
                    Id = Guid.NewGuid(),
                    ContactId = contactZ.Id,
                    CreatedByUserId = contactZ.OwnerId,
                    ValidUntil = DateTime.UtcNow.AddDays(5),
                    Status = OfferStatusEnum.Sent
                }
            );
            await _contextMock.SaveChangesAsync();

            var command = new OfferListCommand
            {
                SortBy = "companyname",
                SortDescending = true,
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _offerServicesMock.GetOfferListAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data!.Items.Count).IsEqualTo(2);
            await Assert.That(result.Data.Items[0].CompanyName).IsEqualTo("Zeta Met");
            await Assert.That(result.Data.Items[1].CompanyName).IsEqualTo("Alfa Stal");
        }
    }
}
