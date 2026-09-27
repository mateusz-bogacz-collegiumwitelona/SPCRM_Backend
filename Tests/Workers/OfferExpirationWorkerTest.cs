using Domain.Enum;
using Domain.Models;
using Infrastructure;
using Infrastructure.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Testcontainers.PostgreSql;
using Worker.Workers;

namespace Tests.Workers
{
    public class OfferExpirationWorkerTest
    {
        private AppDbContext _contextMock = null!;
        private static PostgreSqlContainer _dbContainer = null!;
        private static string _connectionString = null!;
        private string _currentSchema = null!;

        private ILogger<OfferExpirationWorker> _loggerMock = null!;
        private OfferExpirationWorker _worker = null!;

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

            _loggerMock = new LoggerFactory().CreateLogger<OfferExpirationWorker>();
            _worker = new OfferExpirationWorker(_contextMock, _loggerMock);
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

        private async Task<(Company Company, Contact Contact, Currency Currency)> SeedCompanyAndContactAsync()
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

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Polski Złoty",
                Code = "PLN",
                DecimalPlaces = 2,
                IsDeleted = false
            };
            _contextMock.Currencies.Add(currency);

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = "Stal-Met",
                NIP = "1234567890",
                OwnerId = user.Id
            };
            _contextMock.Companies.Add(company);

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Jan",
                LastName = "Kowalski",
                CompanyId = company.Id,
                OwnerId = user.Id,
                IsPrimary = true,
                Owner = user
            };
            _contextMock.Contacts.Add(contact);

            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            return (company, contact, currency);
        }

        private async Task<Offer> SeedOfferAsync(
            Contact contact,
            Currency currency,
            OfferStatusEnum status,
            DateTime validUntil,
            string? name = null)
        {
            var offer = new Offer
            {
                Id = Guid.NewGuid(),
                Name = name ?? $"OF/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
                ContactId = contact.Id,
                CreatedByUserId = contact.OwnerId,
                CurrencyId = currency.Id,
                ValidUntil = validUntil,
                Status = status,
                IsDeleted = false
            };

            _contextMock.Offers.Add(offer);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            return offer;
        }

        // ─── ExpireOldOffersAsync ────────────────────────────────────────────────────────────

        [Test]
        public async Task ExpireOldOffersAsync_WhenOfferIsSentAndValidUntilPassed_MarksOfferAsExpired()
        {
            // Arrange
            var (_, contact, currency) = await SeedCompanyAndContactAsync();
            var pastDate = DateTime.UtcNow.AddDays(-2);

            var offer = await SeedOfferAsync(contact, currency, OfferStatusEnum.Sent, pastDate);

            // Act
            await _worker.ExpireOldOffersAsync();

            // Assert
            var updatedOffer = await _contextMock.Offers
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == offer.Id);

            await Assert.That(updatedOffer).IsNotNull();
            await Assert.That(updatedOffer!.Status).IsEqualTo(OfferStatusEnum.Expired);
        }

        [Test]
        public async Task ExpireOldOffersAsync_WhenOfferIsSentButValidUntilInFuture_DoesNotChangeStatus()
        {
            // Arrange
            var (_, contact, currency) = await SeedCompanyAndContactAsync();
            var futureDate = DateTime.UtcNow.AddDays(5);

            var offer = await SeedOfferAsync(contact, currency, OfferStatusEnum.Sent, futureDate);

            // Act
            await _worker.ExpireOldOffersAsync();

            // Assert
            var updatedOffer = await _contextMock.Offers
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == offer.Id);

            await Assert.That(updatedOffer).IsNotNull();
            await Assert.That(updatedOffer!.Status).IsEqualTo(OfferStatusEnum.Sent);
        }

        [Test]
        [Arguments(OfferStatusEnum.Accepted)]
        [Arguments(OfferStatusEnum.Rejected)]
        [Arguments(OfferStatusEnum.Expired)]
        public async Task ExpireOldOffersAsync_WhenOfferDatePassedButStatusIsNotSent_DoesNotChangeStatus(OfferStatusEnum initialStatus)
        {
            // Arrange
            var (_, contact, currency) = await SeedCompanyAndContactAsync();
            var pastDate = DateTime.UtcNow.AddDays(-3);

            var offer = await SeedOfferAsync(contact, currency, initialStatus, pastDate);

            // Act
            await _worker.ExpireOldOffersAsync();

            // Assert
            var updatedOffer = await _contextMock.Offers
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == offer.Id);

            await Assert.That(updatedOffer).IsNotNull();
            await Assert.That(updatedOffer!.Status).IsEqualTo(initialStatus);
        }

        [Test]
        public async Task ExpireOldOffersAsync_WhenMultipleOffersExist_OnlyExpiresMatchingOffers()
        {
            // Arrange
            var (_, contact, currency) = await SeedCompanyAndContactAsync();

            var expiredOffer = await SeedOfferAsync(contact, currency, OfferStatusEnum.Sent, DateTime.UtcNow.AddDays(-1), "OF/TO_EXPIRE");
            var activeOffer = await SeedOfferAsync(contact, currency, OfferStatusEnum.Sent, DateTime.UtcNow.AddDays(3), "OF/STAYS_SENT");
            var acceptedOffer = await SeedOfferAsync(contact, currency, OfferStatusEnum.Accepted, DateTime.UtcNow.AddDays(-5), "OF/STAYS_ACCEPTED");
            var rejectedOffer = await SeedOfferAsync(contact, currency, OfferStatusEnum.Rejected, DateTime.UtcNow.AddDays(-5), "OF/STAYS_REJECTED");
            var alreadyExpired = await SeedOfferAsync(contact, currency, OfferStatusEnum.Expired, DateTime.UtcNow.AddDays(-10), "OF/ALREADY_EXPIRED");

            // Act
            await _worker.ExpireOldOffersAsync();

            // Assert
            var allOffers = await _contextMock.Offers
                .AsNoTracking()
                .ToListAsync();

            var resultExpiredOffer = allOffers.First(o => o.Id == expiredOffer.Id);
            var resultActiveOffer = allOffers.First(o => o.Id == activeOffer.Id);
            var resultAcceptedOffer = allOffers.First(o => o.Id == acceptedOffer.Id);
            var resultRejectedOffer = allOffers.First(o => o.Id == rejectedOffer.Id);
            var resultAlreadyExpired = allOffers.First(o => o.Id == alreadyExpired.Id);

            await Assert.That(resultExpiredOffer.Status).IsEqualTo(OfferStatusEnum.Expired);
            await Assert.That(resultActiveOffer.Status).IsEqualTo(OfferStatusEnum.Sent);
            await Assert.That(resultAcceptedOffer.Status).IsEqualTo(OfferStatusEnum.Accepted);
            await Assert.That(resultRejectedOffer.Status).IsEqualTo(OfferStatusEnum.Rejected);
            await Assert.That(resultAlreadyExpired.Status).IsEqualTo(OfferStatusEnum.Expired);
        }

        [Test]
        public async Task ExpireOldOffersAsync_WhenNoOffersToExpire_CompletesCleanlyWithoutModifications()
        {
            // Arrange
            var (_, contact, currency) = await SeedCompanyAndContactAsync();
            await SeedOfferAsync(contact, currency, OfferStatusEnum.Sent, DateTime.UtcNow.AddDays(10));

            // Act
            await _worker.ExpireOldOffersAsync();

            // Assert
            var countExpired = await _contextMock.Offers.CountAsync(o => o.Status == OfferStatusEnum.Expired);
            await Assert.That(countExpired).IsEqualTo(0);
        }

        [Test]
        public async Task ExpireOldOffersAsync_WhenOfferIsSoftDeleted_DoesNotProcessIt()
        {
            // Arrange
            var (_, contact, currency) = await SeedCompanyAndContactAsync();
            var pastDate = DateTime.UtcNow.AddDays(-2);

            var deletedOffer = new Offer
            {
                Id = Guid.NewGuid(),
                Name = "OF/DELETED",
                ContactId = contact.Id,
                CreatedByUserId = contact.OwnerId,
                CurrencyId = currency.Id,
                ValidUntil = pastDate,
                Status = OfferStatusEnum.Sent,
                IsDeleted = true
            };

            _contextMock.Offers.Add(deletedOffer);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            // Act
            await _worker.ExpireOldOffersAsync();

            // Assert 
            var offerInDb = await _contextMock.Offers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.Id == deletedOffer.Id);

            await Assert.That(offerInDb).IsNotNull();
            await Assert.That(offerInDb!.Status).IsEqualTo(OfferStatusEnum.Sent);
        }
    }
}
