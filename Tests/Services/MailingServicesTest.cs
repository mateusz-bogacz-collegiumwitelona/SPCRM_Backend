using Domain.Comunication;
using Domain.Constants;
using Domain.Models;
using Infrastructure;
using Infrastructure.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Services.Command.Mailing;
using Services.Command.Support;
using Services.Interfaces;
using Services.Services;
using Testcontainers.PostgreSql;

namespace Tests.Services
{
    public class MailingServicesTest
    {

        protected AppDbContext _contextMock = null!;

        private static PostgreSqlContainer _dbContainer = null!;
        private static string _connectionString = null!;

        protected MailingServices _supportServicesMock = null!;
        protected ILogger<MailingServices> _loggerMock = null!;
        protected FakeEmailSender _fakeEmailSender = null!;

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

            _loggerMock = new LoggerFactory().CreateLogger<MailingServices>();
            _fakeEmailSender = new FakeEmailSender();

            var inMemorySettings = new Dictionary<string, string> {
                {"SUPPORT_EMAIL", "support@mojafirma.pl"}
            };

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings!)
                .Build();

            _supportServicesMock = new MailingServices(
                _contextMock,
                configuration,
                _fakeEmailSender,
                _loggerMock
            );
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

        // ─── SendEmailToSupport ─────────────────────────────────────────────────

        [Test]
        public async Task SendEmailToSupport_WhenUserDoesNotExist_Returns404NotFound()
        {
            // Arrange
            var request = new SupportEmailCommand
            {
                Email = "nonexistent@test.pl",
                Title = "Problem",
                Message = "Nie działa"
            };

            // Act
            var result = await _supportServicesMock.SendEmailToSupport(request);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.UserNotFound);
            await Assert.That(_fakeEmailSender.CallCount).IsEqualTo(0);
        }

        [Test]
        public async Task SendEmailToSupport_WhenUserExists_BuildsDomainAndPassesToSender()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var email = $"user_{uniqueSuffix}@test.pl";

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"U_{uniqueSuffix}",
                NormalizedUserName = $"U_{uniqueSuffix}",
                Email = email,
                NormalizedEmail = email.ToUpper(),
                FirstName = "Adam",
                LastName = "Kowalski"
            };

            _contextMock.Users.Add(user);
            await _contextMock.SaveChangesAsync();

            var request = new SupportEmailCommand
            {
                Email = email,
                Title = "Błąd w module X",
                Message = "Krótki opis błędu"
            };

            // Act
            var result = await _supportServicesMock.SendEmailToSupport(request);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(_fakeEmailSender.CallCount).IsEqualTo(1);
            await Assert.That(_fakeEmailSender.SentReport).IsNotNull();

            var sentReport = _fakeEmailSender.SentReport!;

            await Assert.That(sentReport.UserEmail).IsEqualTo(email);
            await Assert.That(sentReport.UserName).IsEqualTo("Adam");
            await Assert.That(sentReport.UserSurname).IsEqualTo("Kowalski");
            await Assert.That(sentReport.Title).IsEqualTo("Błąd w module X");
            await Assert.That(sentReport.SupportEmail).IsEqualTo("support@mojafirma.pl");
            await Assert.That(sentReport.Time).IsNotNull();
        }

        [Test]
        public async Task SendProductMailingAsync_WhenClientOrProductMissing_Returns404NotFound()
        {
            var command = new MailingCommand
            {
                To = [Guid.NewGuid()],
                Products = [
                    new MailingProductCommand { ProductId = Guid.NewGuid(), Quantity = 10 }
                ],
                Language = "pl"
            };

            // Act
            var result = await _supportServicesMock.SendProductMailingAsync(command, Guid.NewGuid());

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.ClientNotFound);
        }

        [Test]
        public async Task SendProductMailingAsync_WhenDataIsValid_SavesOffersAndQueuesEmail()
        {
            // Arrange
            var ownerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = "TestUser",
                Email = "test@test.pl",
                FirstName = "Test",
                LastName = "User"
            };
            _contextMock.Users.Add(owner);

            var company = new Company
            {
                Id = Guid.NewGuid(),
                NIP = "12345678901",
                Name = "Testowa Firma Sp. z o.o.",
                OwnerId = ownerId,
                Owner = owner
            };
            _contextMock.Companies.Add(company);

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Code = "PLN",
                Name = "Polski Złoty",
                DecimalPlaces = 2
            };

            _contextMock.Currencies.Add(currency);

            var unit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = "Sztuka",
                Symbol = "szt."
            };
            _contextMock.UnitsOfMeasure.Add(unit);

            var steelGrade = new SteelGrade
            {
                Id = Guid.NewGuid(),
                Name = "S235"
            };

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Blacha stalowa",
                SteelGrade = steelGrade,
                SteelGradeId = steelGrade.Id,
                Thickness = 10,
                Width = 1000,
                Length = 2000,
                Weight = 15000,
                UnitId = unit.Id,
                PricePerUnit = 500000,
                StockQuantity = 100,
                Category = Domain.Enum.ProductCategoryEnum.Profile,
                CurrencyId = currency.Id
            };
            _contextMock.Products.Add(product);

            var contactId = Guid.NewGuid();

            var contact = new Contact
            {
                Id = contactId,
                FirstName = "Jan",
                LastName = "Nowak",
                IsPrimary = true,
                CompanyId = company.Id,
                OwnerId = ownerId,
                Owner = owner,
                ContactDetails = new List<ContactDetail>
                {
                    new ContactDetail
                    {
                        Id = Guid.NewGuid(),
                        Type = Domain.Enum.ContactDetailTypeEnum.EMAIL,
                        Value = "jan.nowak@test.pl",
                        IsPrimary = true
                    }
                }
            };
            _contextMock.Contacts.Add(contact);
            try
            {
                await _contextMock.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                var innerMessage = ex.InnerException?.Message;
                throw new Exception($"SQL Save Changes Error: {innerMessage}", ex);
            }

            var command = new MailingCommand
            {
                To = [contactId],
                Products = [
                    new MailingProductCommand
                    {
                        ProductId = product.Id,
                        Quantity = 5,
                        CurrencyCode = "PLN"
                    }
                ],
                Language = "pl"
            };

            // Act
            var result = await _supportServicesMock.SendProductMailingAsync(command, ownerId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var savedOffer = await _contextMock.Offers
                .Include(o => o.Products)
                .FirstOrDefaultAsync(o => o.ContactId == contactId);

            await Assert.That(savedOffer).IsNotNull();
            await Assert.That(savedOffer!.CreatedByUserId).IsEqualTo(ownerId);
            await Assert.That(savedOffer.Status).IsEqualTo(Domain.Enum.OfferStatusEnum.Sent);
            await Assert.That(savedOffer.Products).Count().IsEqualTo(1);

            var savedItem = savedOffer.Products.First();
            await Assert.That(savedItem.ProductId).IsEqualTo(product.Id);
            await Assert.That(savedItem.Quantity).IsEqualTo(5);
            await Assert.That(savedItem.QuotedPrice).IsEqualTo(500000);
        }
    }

    public class FakeEmailSender : IEmailSender
    {
        public ReportDomain? SentReport { get; private set; }
        public int CallCount { get; private set; } = 0;

        public Task SendReportEmailAsync(ReportDomain report)
        {
            SentReport = report;
            CallCount++;
            return Task.CompletedTask;
        }

        public Task SendProductMailingAsync(MailingOfferDomain domain)
        {
            return Task.CompletedTask;
        }
    }
}
