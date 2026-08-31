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
               .LogTo(Console.WriteLine, LogLevel.Information)
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
                    Name = $"OF/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
                    Id = Guid.NewGuid(),
                    ContactId = contact.Id,
                    CreatedByUserId = contact.OwnerId,
                    ValidUntil = DateTime.UtcNow.AddDays(10),
                    Status = OfferStatusEnum.Sent
                },
                new()
                {
                    Name = $"OF/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
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
                    Name = $"OF/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
                    Id = Guid.NewGuid(),
                    ContactId = contactMatching.Id,
                    CreatedByUserId = contactMatching.OwnerId,
                    ValidUntil = DateTime.UtcNow.AddDays(5),
                    Status = OfferStatusEnum.Sent
                },
                new Offer
                {
                    Name = $"OF/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
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
                    Name = $"OF/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
                    Id = Guid.NewGuid(),
                    ContactId = contact.Id,
                    CreatedByUserId = contact.OwnerId,
                    ValidUntil = DateTime.UtcNow.AddDays(5),
                    Status = OfferStatusEnum.Sent,
                    IsDeleted = false
                },
                new Offer
                {
                    Name = $"OF/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
                    Id = Guid.NewGuid(),
                    ContactId = contact.Id,
                    CreatedByUserId = contact.OwnerId,
                    ValidUntil = DateTime.UtcNow.AddDays(-5),
                    Status = OfferStatusEnum.Expired,
                    IsDeleted = false
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
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.Items.Count).IsEqualTo(1);
            await Assert.That(result.Data.Items.First().Status).IsEqualTo(OfferStatusEnum.Expired.ToString());
        }

        [Test]
        public async Task GetOfferListAsync_FiltersCorrectly_ByValidUntilDateRange()
        {
            // Arrange
            var (_, contact) = await SeedCompanyAndContactAsync();
            var baseDate = DateTime.UtcNow.AddDays(30);

            _contextMock.Offers.AddRange(
                new Offer
                {
                    Name = "OF/RANGE/PAST",
                    Id = Guid.NewGuid(),
                    ContactId = contact.Id,
                    CreatedByUserId = contact.OwnerId,
                    ValidUntil = baseDate.AddDays(-10),
                    Status = OfferStatusEnum.Sent,
                    IsDeleted = false
                },
                new Offer
                {
                    Name = "OF/RANGE/TARGET",
                    Id = Guid.NewGuid(),
                    ContactId = contact.Id,
                    CreatedByUserId = contact.OwnerId,
                    ValidUntil = baseDate,
                    Status = OfferStatusEnum.Sent,
                    IsDeleted = false
                },
                new Offer
                {
                    Name = "OF/RANGE/FUTURE",
                    Id = Guid.NewGuid(),
                    ContactId = contact.Id,
                    CreatedByUserId = contact.OwnerId,
                    ValidUntil = baseDate.AddDays(10),
                    Status = OfferStatusEnum.Sent,
                    IsDeleted = false
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
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.Items.Count).IsEqualTo(1);
            await Assert.That(result.Data.Items.First().OfferName).IsEqualTo("OF/RANGE/TARGET");
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
                    Name = $"OF/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
                    Id = Guid.NewGuid(),
                    ContactId = contactA.Id,
                    CreatedByUserId = contactA.OwnerId,
                    ValidUntil = DateTime.UtcNow.AddDays(5),
                    Status = OfferStatusEnum.Sent
                },
                new Offer
                {
                    Name = $"OF/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
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

        [Test]
        public async Task GetOfferListAsync_FiltersCorrectly_WhenIsExpiredIsTrue()
        {
            // Arrange
            var (_, contact) = await SeedCompanyAndContactAsync();

            _contextMock.Offers.AddRange(
                new Offer
                {
                    Id = Guid.NewGuid(),
                    Name = "OF/ACTIVE",
                    ContactId = contact.Id,
                    CreatedByUserId = contact.OwnerId,
                    ValidUntil = DateTime.UtcNow.AddDays(5),
                    Status = OfferStatusEnum.Sent
                },
                new Offer
                {
                    Id = Guid.NewGuid(),
                    Name = "OF/EXPIRED",
                    ContactId = contact.Id,
                    CreatedByUserId = contact.OwnerId,
                    ValidUntil = DateTime.UtcNow.AddDays(-5),
                    Status = OfferStatusEnum.Sent
                }
            );
            await _contextMock.SaveChangesAsync();

            var command = new OfferListCommand
            {
                IsExpired = true,
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _offerServicesMock.GetOfferListAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data!.Items.Count).IsEqualTo(1);
            await Assert.That(result.Data.Items.First().OfferName).IsEqualTo("OF/EXPIRED");
            await Assert.That(result.Data.Items.First().IsExpired).IsTrue();
        }

        // ─── GetOfferListAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetOfferDetailAsync_ReturnsNotFound_WhenOfferDoesNotExist()
        {
            // Arrange
            var nonExistentId = Guid.NewGuid();

            // Act
            var result = await _offerServicesMock.GetOfferDetailAsync(nonExistentId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.OfferNotFound);
            await Assert.That(result.Data).IsNull();
        }

        [Test]
        public async Task GetOfferDetailAsync_ReturnsOfferDetails_WhenOfferExists()
        {
            // Arrange
            var (_, contact) = await SeedCompanyAndContactAsync();
            var validUntilDate = new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);

            var offer = new Offer
            {
                Id = Guid.NewGuid(),
                Name = "OF/2026/08/31/A4F89B",
                ContactId = contact.Id,
                CreatedByUserId = contact.OwnerId,
                ValidUntil = validUntilDate,
                Status = OfferStatusEnum.Accepted
            };

            _contextMock.Offers.Add(offer);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _offerServicesMock.GetOfferDetailAsync(offer.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.OfferId).IsEqualTo(offer.Id);
            await Assert.That(result.Data.OfferName).IsEqualTo("OF/2026/08/31/A4F89B");
            await Assert.That(result.Data.Status).IsEqualTo(OfferStatusEnum.Accepted.ToString());
            await Assert.That(result.Data.ValidUntil).IsEqualTo(validUntilDate);
        }

        [Test]
        public async Task GetOfferDetailAsync_ReturnsNotFound_WhenOfferIsSoftDeleted()
        {
            // Arrange
            var (_, contact) = await SeedCompanyAndContactAsync();

            var offer = new Offer
            {
                Id = Guid.NewGuid(),
                Name = "OF/2026/08/31/DELETED",
                ContactId = contact.Id,
                CreatedByUserId = contact.OwnerId,
                ValidUntil = DateTime.UtcNow.AddDays(7),
                Status = OfferStatusEnum.Sent,
                IsDeleted = true
            };

            _contextMock.Offers.Add(offer);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _offerServicesMock.GetOfferDetailAsync(offer.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.OfferNotFound);
            await Assert.That(result.Data).IsNull();
        }

        // ─── GetOfferClientDetailAsync ──────────────────────────────────────────

        [Test]
        public async Task GetOfferClientDetailAsync_ReturnsNotFound_WhenOfferDoesNotExist()
        {
            // Arrange
            var nonExistentId = Guid.NewGuid();

            // Act
            var result = await _offerServicesMock.GetOfferClientDetailAsync(nonExistentId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.OfferNotFound);
            await Assert.That(result.Data).IsNull();
        }

        [Test]
        public async Task GetOfferClientDetailAsync_ReturnsClientDetails_WhenOfferExists()
        {
            // Arrange
            var (company, contact) = await SeedCompanyAndContactAsync("Huta Stalowa Wola", "Marek", "Nowak");
            contact.JobTitle = "Dyrektor ds. Zakupów";
            await _contextMock.SaveChangesAsync();

            var offer = new Offer
            {
                Id = Guid.NewGuid(),
                Name = "OF/2026/08/31/XYZ123",
                ContactId = contact.Id,
                CreatedByUserId = contact.OwnerId,
                ValidUntil = DateTime.UtcNow.AddDays(7),
                Status = OfferStatusEnum.Sent,
                IsDeleted = false
            };

            _contextMock.Offers.Add(offer);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _offerServicesMock.GetOfferClientDetailAsync(offer.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.ContactId).IsEqualTo(contact.Id);
            await Assert.That(result.Data.ContactFirstName).IsEqualTo("Marek");
            await Assert.That(result.Data.ContactLastName).IsEqualTo("Nowak");
            await Assert.That(result.Data.ContactJobTitle).IsEqualTo("Dyrektor ds. Zakupów");
            await Assert.That(result.Data.CompanyName).IsEqualTo("Huta Stalowa Wola");
        }

        [Test]
        public async Task GetOfferClientDetailAsync_HandlesNullJobTitle_Gracefully()
        {
            // Arrange
            var (company, contact) = await SeedCompanyAndContactAsync("Met-Bud", "Anna", "Kowalska");
            contact.JobTitle = null;
            await _contextMock.SaveChangesAsync();

            var offer = new Offer
            {
                Id = Guid.NewGuid(),
                Name = "OF/2026/08/31/ABC456",
                ContactId = contact.Id,
                CreatedByUserId = contact.OwnerId,
                ValidUntil = DateTime.UtcNow.AddDays(7),
                Status = OfferStatusEnum.Sent,
                IsDeleted = false
            };

            _contextMock.Offers.Add(offer);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _offerServicesMock.GetOfferClientDetailAsync(offer.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.ContactJobTitle).IsEqualTo(string.Empty);
            await Assert.That(result.Data.CompanyName).IsEqualTo("Met-Bud");
        }

        [Test]
        public async Task GetOfferClientDetailAsync_ReturnsNotFound_WhenOfferIsSoftDeleted()
        {
            // Arrange
            var (_, contact) = await SeedCompanyAndContactAsync();

            var offer = new Offer
            {
                Id = Guid.NewGuid(),
                Name = "OF/2026/08/31/DELETED",
                ContactId = contact.Id,
                CreatedByUserId = contact.OwnerId,
                ValidUntil = DateTime.UtcNow.AddDays(7),
                Status = OfferStatusEnum.Sent,
                IsDeleted = true
            };

            _contextMock.Offers.Add(offer);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _offerServicesMock.GetOfferClientDetailAsync(offer.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.OfferNotFound);
            await Assert.That(result.Data).IsNull();
        }

        // ─── GetOfferProductsAsync ──────────────────────────────────────────────

        [Test]
        public async Task GetOfferProductsAsync_ReturnsNotFound_WhenOfferDoesNotExist()
        {
            // Arrange
            var nonExistentId = Guid.NewGuid();
            var command = new SimpleListCommand
            {
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _offerServicesMock.GetOfferProductsAsync(nonExistentId, command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.OfferNotFound);
            await Assert.That(result.Data).IsNull();
        }

        [Test]
        public async Task GetOfferProductsAsync_ReturnsAllProductsPaged_WhenNoSearchTermProvided()
        {
            // Arrange
            var (_, contact) = await SeedCompanyAndContactAsync();

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Polski Złoty",
                Code = "PLN",
                DecimalPlaces = 2,
                IsDeleted = false
            };
            _contextMock.Currencies.Add(currency);

            var steelGrade = new SteelGrade
            {
                Id = Guid.NewGuid(),
                Name = "1.4301",
                Density = 7900,
                IsDeleted = false
            };
            _contextMock.SteelGrades.Add(steelGrade);

            var unit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = "Sztuka",
                Symbol = "szt.",
                BaseMultiplier = 1,
                IsDeleted = false
            };
            _contextMock.UnitsOfMeasure.Add(unit);

            _contextMock.SteelGrades.Add(steelGrade);

            var productA = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Blacha kwasoodporna 2mm",
                SteelGradeId = steelGrade.Id,
                UnitId = unit.Id,
                CurrencyId = currency.Id,
                PricePerUnit = 120000,
                StockQuantity = 50,
                Category = ProductCategoryEnum.Sheet,
                Thickness = 2,
                Width = 1000,
                Length = 2000,
                Weight = 31400,
                IsDeleted = false,
                SteelGrade = steelGrade
            };

            var productB = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Rura kwasoodporna fi 25",
                SteelGradeId = steelGrade.Id,
                UnitId = unit.Id,
                CurrencyId = currency.Id,
                PricePerUnit = 45000,
                StockQuantity = 100,
                Category = ProductCategoryEnum.Pipe,
                Thickness = 2,
                Width = 0,
                Length = 6000,
                Weight = 6900,
                IsDeleted = false,
                SteelGrade = steelGrade
            };

            _contextMock.Products.AddRange(productA, productB);

            var offer = new Offer
            {
                Id = Guid.NewGuid(),
                Name = "OF/2026/08/31/PAGED",
                ContactId = contact.Id,
                CreatedByUserId = contact.OwnerId,
                ValidUntil = DateTime.UtcNow.AddDays(7),
                Status = OfferStatusEnum.Sent,
                IsDeleted = false
            };
            _contextMock.Offers.Add(offer);

            _contextMock.OfferProducts.AddRange(
                new OfferProducts
                {
                    Id = Guid.NewGuid(),
                    OfferId = offer.Id,
                    ProductId = productA.Id,
                    Quantity = 2,
                    QuotedPrice = 115000,
                    CurrencyId = currency.Id,
                    IsDeleted = false
                },
                new OfferProducts
                {
                    Id = Guid.NewGuid(),
                    OfferId = offer.Id,
                    ProductId = productB.Id,
                    Quantity = 10,
                    QuotedPrice = 42000,
                    CurrencyId = currency.Id,
                    IsDeleted = false
                }
            );

            await _contextMock.SaveChangesAsync();

            var command = new SimpleListCommand
            {
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _offerServicesMock.GetOfferProductsAsync(offer.Id, command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.Items.Count).IsEqualTo(2);
            await Assert.That(result.Data.TotalCount).IsEqualTo(2);
        }

        [Test]
        public async Task GetOfferProductsAsync_FiltersBySearchTerm_WithUnaccentAndCaseInsensitivity()
        {
            // Arrange
            var (_, contact) = await SeedCompanyAndContactAsync();

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Euro",
                Code = "EUR",
                DecimalPlaces = 2,
                IsDeleted = false
            };
            _contextMock.Currencies.Add(currency);

            var steelGrade1 = new SteelGrade { Id = Guid.NewGuid(), Name = "S355J2", Density = 7850, IsDeleted = false };
            var steelGrade2 = new SteelGrade { Id = Guid.NewGuid(), Name = "1.4404", Density = 8000, IsDeleted = false };
            _contextMock.SteelGrades.AddRange(steelGrade1, steelGrade2);

            var unit = new UnitOfMeasure { Id = Guid.NewGuid(), Name = "Metr", Symbol = "m", BaseMultiplier = 1, IsDeleted = false };
            _contextMock.UnitsOfMeasure.Add(unit);

            var product1 = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Pręt żebrowany fi 12",
                SteelGradeId = steelGrade1.Id,
                UnitId = unit.Id,
                CurrencyId = currency.Id,
                PricePerUnit = 3500,
                StockQuantity = 200,
                Category = ProductCategoryEnum.Bar,
                Thickness = 0,
                Width = 0,
                Length = 12000,
                Weight = 10660,
                IsDeleted = false,
                SteelGrade = steelGrade1
            };

            var product2 = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Kątownik zimnogięty 50x50",
                SteelGradeId = steelGrade2.Id,
                UnitId = unit.Id,
                CurrencyId = currency.Id,
                PricePerUnit = 8900,
                StockQuantity = 80,
                Category = ProductCategoryEnum.Pipe,
                Thickness = 4,
                Width = 50,
                Length = 6000,
                Weight = 18000,
                IsDeleted = false,
                SteelGrade = steelGrade2
            };

            _contextMock.Products.AddRange(product1, product2);

            var offer = new Offer
            {
                Id = Guid.NewGuid(),
                Name = "OF/2026/08/31/SEARCH",
                ContactId = contact.Id,
                CreatedByUserId = contact.OwnerId,
                ValidUntil = DateTime.UtcNow.AddDays(7),
                Status = OfferStatusEnum.Sent,
                IsDeleted = false
            };
            _contextMock.Offers.Add(offer);

            _contextMock.OfferProducts.AddRange(
                new OfferProducts
                {
                    Id = Guid.NewGuid(),
                    OfferId = offer.Id,
                    ProductId = product1.Id,
                    Quantity = 20,
                    QuotedPrice = 3300,
                    CurrencyId = currency.Id,
                    IsDeleted = false
                },
                new OfferProducts
                {
                    Id = Guid.NewGuid(),
                    OfferId = offer.Id,
                    ProductId = product2.Id,
                    Quantity = 5,
                    QuotedPrice = 8500,
                    CurrencyId = currency.Id,
                    IsDeleted = false
                }
            );

            await _contextMock.SaveChangesAsync();

            var command = new SimpleListCommand
            {
                SearchTerm = "zebrowany",
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _offerServicesMock.GetOfferProductsAsync(offer.Id, command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.Items.Count).IsEqualTo(1);
            await Assert.That(result.Data.Items.First().ProductName).IsEqualTo("Pręt żebrowany fi 12");
            await Assert.That(result.Data.Items.First().SteelGrade).IsEqualTo("S355J2");
            await Assert.That(result.Data.Items.First().QuotedPrice).IsEqualTo(3300);
            await Assert.That(result.Data.Items.First().CurrencyCode).IsEqualTo("EUR");
        }

        [Test]
        public async Task GetOfferProductsAsync_ReturnsEmptyList_WhenOfferHasNoProducts()
        {
            // Arrange
            var (_, contact) = await SeedCompanyAndContactAsync();

            var offer = new Offer
            {
                Id = Guid.NewGuid(),
                Name = "OF/2026/08/31/EMPTY",
                ContactId = contact.Id,
                CreatedByUserId = contact.OwnerId,
                ValidUntil = DateTime.UtcNow.AddDays(7),
                Status = OfferStatusEnum.Sent,
                IsDeleted = false
            };
            _contextMock.Offers.Add(offer);
            await _contextMock.SaveChangesAsync();

            var command = new SimpleListCommand
            {
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _offerServicesMock.GetOfferProductsAsync(offer.Id, command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.Items.Count).IsEqualTo(0);
            await Assert.That(result.Data.TotalCount).IsEqualTo(0);
        }

        [Test]
        public async Task GetOfferProductsAsync_FiltersOutSoftDeletedOfferProducts()
        {
            // Arrange
            var (_, contact) = await SeedCompanyAndContactAsync();

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Polski Złoty",
                Code = "PLN",
                DecimalPlaces = 2,
                IsDeleted = false
            };
            _contextMock.Currencies.Add(currency);

            var steelGrade = new SteelGrade { Id = Guid.NewGuid(), Name = "S235JR", Density = 7850, IsDeleted = false };
            _contextMock.SteelGrades.Add(steelGrade);

            var unit = new UnitOfMeasure { Id = Guid.NewGuid(), Name = "Sztuka", Symbol = "szt.", BaseMultiplier = 1, IsDeleted = false };
            _contextMock.UnitsOfMeasure.Add(unit);

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Dwuteownik HEB 100",
                SteelGradeId = steelGrade.Id,
                UnitId = unit.Id,
                CurrencyId = currency.Id,
                PricePerUnit = 200000,
                StockQuantity = 30,
                Category = ProductCategoryEnum.Profile,
                Thickness = 6,
                Width = 100,
                Length = 12000,
                Weight = 244800,
                IsDeleted = false,
                SteelGrade = steelGrade
            };
            _contextMock.Products.Add(product);

            var offer = new Offer
            {
                Id = Guid.NewGuid(),
                Name = "OF/2026/08/31/SOFTDELETE",
                ContactId = contact.Id,
                CreatedByUserId = contact.OwnerId,
                ValidUntil = DateTime.UtcNow.AddDays(7),
                Status = OfferStatusEnum.Sent,
                IsDeleted = false
            };
            _contextMock.Offers.Add(offer);

            _contextMock.OfferProducts.Add(
                new OfferProducts
                {
                    Id = Guid.NewGuid(),
                    OfferId = offer.Id,
                    ProductId = product.Id,
                    Quantity = 3,
                    QuotedPrice = 195000,
                    CurrencyId = currency.Id,
                    IsDeleted = true
                }
            );

            await _contextMock.SaveChangesAsync();

            var command = new SimpleListCommand
            {
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _offerServicesMock.GetOfferProductsAsync(offer.Id, command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.Items.Count).IsEqualTo(0);
            await Assert.That(result.Data.TotalCount).IsEqualTo(0);
        }
    }
}
