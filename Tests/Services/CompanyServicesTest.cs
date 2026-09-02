using Domain.Enum;
using Domain.Models;
using Infrastructure;
using Infrastructure.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using Npgsql;
using Services.Command.Company;
using Services.Interfaces;
using Services.Services;
using Testcontainers.PostgreSql;

namespace Tests.Services
{
    public class CompanyServicesTest
    {
        protected AppDbContext _contextMock = null!;

        protected CompanyServices _companyServicesMock = null!;
        protected ILogger<CompanyServices> _loggerMock = null!;

        private static PostgreSqlContainer _dbContainer = null!;
        private static string _connectionString = null!;

        private int SRID = 4326; // WGS 84
        private string _currentSchema = null!;
        protected IEntityAuthorizationService _entityAuthMock = null!;

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

            _loggerMock = new LoggerFactory().CreateLogger<CompanyServices>();
            _entityAuthMock = new EntityAuthorizationService(_contextMock);

            _companyServicesMock = new CompanyServices(_contextMock, _loggerMock, _entityAuthMock);
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

        // ─── Map ─────────────────────────────────────────────────

        [Test]
        public async Task Map_WhenSearchTermIsNull_ReturnsCompaniesWithMappedCoordinates()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var owner = new ApplicationUser
            {
                UserName = $"User_{uniqueSuffix}",
                NormalizedUserName = $"USER_{uniqueSuffix}",
                Email = $"user_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"USER_{uniqueSuffix}@TEST.PL",
                FirstName = "Jan",
                LastName = "Kowalski",
                EmailConfirmed = true
            };

            var company = new Company
            {
                Name = $"TestCompany_{uniqueSuffix}",
                NIP = "1234567890",
                Owner = owner,
            };

            var address = new CompanyAdress
            {
                Company = company,
                City = "Warszawa",
                Street = "Złota 44",
                ZipCode = "00-120",
                Location = new Point(21.0122, 52.2297) { SRID = SRID },
                AddressType = AddressTypeEnum.Headquarters,
                IsDeleted = false
            };

            _contextMock.Companies.Add(company);
            _contextMock.CompanyAdresses.Add(address);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _companyServicesMock.Map(searchTerm: null);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var mappedCompany = result.Data.FirstOrDefault(c => c.Name == company.Name);

            await Assert.That(mappedCompany).IsNotNull();
            await Assert.That(mappedCompany!.City).IsEqualTo("Warszawa");
            await Assert.That(mappedCompany.Latitude).IsEqualTo(52.2297);
            await Assert.That(mappedCompany.Longitude).IsEqualTo(21.0122);
        }

        [Test]
        public async Task Map_WhenSearchTermProvided_FiltersResultsCorrectly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var owner = new ApplicationUser
            {
                UserName = $"User_{uniqueSuffix}",
                NormalizedUserName = $"USER_{uniqueSuffix}",
                Email = $"user_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"USER_{uniqueSuffix}@TEST.PL",
                FirstName = "Jan",
                LastName = "Kowalski",
                EmailConfirmed = true
            };

            var companyToFind = new Company
            {
                Name = $"Apple_{uniqueSuffix}",
                NIP = "999888777",
                Owner = owner
            };

            var addressToFind = new CompanyAdress
            {
                Company = companyToFind,
                City = "Kraków",
                Street = "Rynek Główny 1",
                ZipCode = "30-001",
                IsDeleted = false,
                Location = new Point(19.9449, 50.0646) { SRID = SRID }
            };

            var companyToIgnore = new Company
            {
                Name = $"Microsoft_{uniqueSuffix}",
                NIP = "111222333",
                Owner = owner
            };

            var addressToIgnore = new CompanyAdress
            {
                Company = companyToIgnore,
                City = "Gdańsk",
                Street = "Gdańsk 1",
                ZipCode = "80-001",
                IsDeleted = false,
                Location = new Point(18.6466, 54.3520) { SRID = SRID }
            };

            _contextMock.CompanyAdresses.AddRange(addressToFind, addressToIgnore);
            await _contextMock.SaveChangesAsync();

            // Act 
            var result = await _companyServicesMock.Map("99888");

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            await Assert.That(result.Data.Any(c => c.Name == companyToFind.Name)).IsTrue();
            await Assert.That(result.Data.Any(c => c.Name == companyToIgnore.Name)).IsFalse();
        }

        // ─── Details ─────────────────────────────────────────────────

        [Test]
        public async Task Details_WhenCompanyDoesNotExist_Returns404NotFound()
        {
            // Arrange
            var nonExistentCompanyId = Guid.NewGuid();
            var randomUserId = Guid.NewGuid();

            // Act
            var result = await _companyServicesMock.Details(nonExistentCompanyId, randomUserId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.Message).IsEqualTo("Company not found");
        }

        [Test]
        public async Task Details_WhenUserIsOwner_ReturnsCompanyWithIsYourTrue()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"Owner_{uniqueSuffix}",
                NormalizedUserName = $"OWNER_{uniqueSuffix}",
                Email = $"owner_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"OWNER_{uniqueSuffix}@TEST.PL",
                FirstName = "Jan",
                LastName = "Kowalski",
                EmailConfirmed = true
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"MyCompany_{uniqueSuffix}",
                NIP = "1112223334",
                OwnerId = ownerId,
                Owner = owner
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _companyServicesMock.Details(company.Id, ownerId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();

            await Assert.That(result.Data!.IsYour).IsTrue();
            await Assert.That(result.Data.Name).IsEqualTo(company.Name);
        }

        // ─── GetCompanyAddresses ─────────────────────────────────────────────────

        [Test]
        public async Task GetCompanyAddresses_WhenCompanyHasAddresses_ReturnsMappedCoordinatesAndFiltersProperly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var owner = new ApplicationUser
            {
                UserName = $"User_{uniqueSuffix}",
                NormalizedUserName = $"USER_{uniqueSuffix}",
                Email = $"user_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"USER_{uniqueSuffix}@TEST.PL",
                FirstName = "Jan",
                LastName = "Kowalski"
            };

            var targetCompany = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Target_{uniqueSuffix}",
                NIP = "111",
                Owner = owner
            };

            var otherCompany = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Other_{uniqueSuffix}",
                NIP = "222",
                Owner = owner
            };

            var targetAddress = new CompanyAdress
            {
                CompanyId = targetCompany.Id,
                Company = targetCompany,
                City = "Kraków",
                Street = "Floriańska 1",
                ZipCode = "30-001",
                Location = new Point(19.9383, 50.0614) { SRID = SRID },
                AddressType = AddressTypeEnum.Headquarters,
                IsDeleted = false
            };

            var otherAddress = new CompanyAdress
            {
                CompanyId = otherCompany.Id,
                Company = otherCompany,
                City = "Warszawa",
                Street = "Złota",
                ZipCode = "00-120",
                AddressType = AddressTypeEnum.Branch,
                IsDeleted = false
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.AddRange(targetCompany, otherCompany);
            _contextMock.CompanyAdresses.AddRange(targetAddress, otherAddress);
            await _contextMock.SaveChangesAsync();

            var command = new CompanyCommand
            {
                PageNumber = 1,
                PageSize = 10,
                CompanyId = targetCompany.Id
            };

            // Act
            var result = await _companyServicesMock.GetCompanyAddresses(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var items = result.Data!.Items;
            await Assert.That(items).Count().IsEqualTo(1);

            var mappedAddress = items.First();
            await Assert.That(mappedAddress.City).IsEqualTo("Kraków");
            await Assert.That(mappedAddress.Longitude).IsEqualTo(19.9383);
            await Assert.That(mappedAddress.Latitude).IsEqualTo(50.0614);
        }

        [Test]
        public async Task GetCompanyAddresses_WhenPaginationProvided_ReturnsCorrectPageSize()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var owner = new ApplicationUser
            {
                UserName = $"User_{uniqueSuffix}",
                NormalizedUserName = $"USER_{uniqueSuffix}",
                Email = $"user_{uniqueSuffix}@test.pl",
                FirstName = "Jan",
                LastName = "Kowalski"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Company_{uniqueSuffix}",
                NIP = "123",
                Owner = owner
            };

            var adresses = new List<CompanyAdress>
            {
                new() {
                    CompanyId = company.Id,
                    Company = company,
                    City = "City1",
                    Street = "Str1",
                    ZipCode = "1",
                    AddressType = AddressTypeEnum.Headquarters,
                    IsDeleted = false
                },
                new() {
                    CompanyId = company.Id,
                    Company = company,
                    City = "City2",
                    Street = "Str2",
                    ZipCode = "2",
                    AddressType = AddressTypeEnum.Branch,
                    IsDeleted = false
                },
                new() {
                    CompanyId = company.Id,
                    Company = company,
                    City = "City3", Street = "Str3",
                    ZipCode = "3",
                    AddressType = AddressTypeEnum.Branch,
                    IsDeleted = false
                }
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.CompanyAdresses.AddRange(adresses);
            await _contextMock.SaveChangesAsync();

            var command = new CompanyCommand
            {
                PageNumber = 1,
                PageSize = 2,
                CompanyId = company.Id
            };

            // Act
            var result = await _companyServicesMock.GetCompanyAddresses(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            await Assert.That(result.Data!.Items).Count().IsEqualTo(2);

            await Assert.That(result.Data.TotalCount).IsEqualTo(3);
        }

        // ─── GetCompanyListAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetCompanyListAsync_ValidatesHeadquartersAndOwnerVisibility()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var myUserId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();

            var me = new ApplicationUser
            {
                Id = myUserId,
                UserName = $"Me_{uniqueSuffix}",
                Email = $"me_{uniqueSuffix}@test.pl",
                FirstName = "Jan",
                LastName = "Kowalski"
            };

            var other = new ApplicationUser
            {
                Id = otherUserId,
                UserName =
                $"Other_{uniqueSuffix}",
                Email = $"other_{uniqueSuffix}@test.pl",
                FirstName = "Anna",
                LastName = "Nowak"
            };

            var myCompanyWithHq = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"MyHqCompany_{uniqueSuffix}",
                NIP = "111",
                OwnerId = myUserId,
                Owner = me
            };

            var myHqAddress = new CompanyAdress
            {
                CompanyId = myCompanyWithHq.Id,
                Company = myCompanyWithHq,
                City = "Kraków",
                Street = "A",
                ZipCode = "1",
                AddressType = AddressTypeEnum.Headquarters
            };

            var myCompanyWithoutHq = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"MyBranchCompany_{uniqueSuffix}",
                NIP = "222",
                OwnerId = myUserId,
                Owner = me
            };

            var myBranchAddress = new CompanyAdress
            {
                CompanyId = myCompanyWithoutHq.Id,
                Company = myCompanyWithoutHq,
                City = "Warszawa",
                Street = "B",
                ZipCode = "2",
                AddressType = AddressTypeEnum.Branch
            };

            var otherCompanyWithHq = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"OtherHqCompany_{uniqueSuffix}",
                NIP = "333",
                OwnerId = otherUserId,
                Owner = other
            };

            var otherHqAddress = new CompanyAdress
            {
                CompanyId = otherCompanyWithHq.Id,
                Company = otherCompanyWithHq,
                City = "Gdańsk",
                Street = "C",
                ZipCode = "3",
                AddressType = AddressTypeEnum.Headquarters
            };

            _contextMock.Users.AddRange(me, other);

            _contextMock.Companies.AddRange(
                myCompanyWithHq,
                myCompanyWithoutHq,
                otherCompanyWithHq
                );

            _contextMock.CompanyAdresses.AddRange(
                myHqAddress,
                myBranchAddress,
                otherHqAddress
                );

            await _contextMock.SaveChangesAsync();

            var command = new CompanyListCommand
            {
                PageNumber = 1,
                PageSize = 10,
                UserId = myUserId
            };

            // Act
            var result = await _companyServicesMock.GetCompanyListAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();

            var returnedCompanies = result.Data!.Items
                .Where(c => c.Name.Contains(uniqueSuffix))
                .ToList();

            await Assert.That(returnedCompanies).Count().IsEqualTo(2);

            var myReturnedCompany = returnedCompanies.First(c => c.Id == myCompanyWithHq.Id);
            await Assert.That(myReturnedCompany.IsYour).IsTrue();
            await Assert.That(myReturnedCompany.OwnerFirstName).IsNull();
            await Assert.That(myReturnedCompany.OwnerLastName).IsNull();

            var otherReturnedCompany = returnedCompanies.First(c => c.Id == otherCompanyWithHq.Id);
            await Assert.That(otherReturnedCompany.IsYour).IsFalse();
            await Assert.That(otherReturnedCompany.OwnerFirstName).IsEqualTo("Anna");
            await Assert.That(otherReturnedCompany.OwnerLastName).IsEqualTo("Nowak");
        }

        [Test]
        public async Task GetCompanyListAsync_MapsLastDealDateCorrectly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userId = Guid.NewGuid();

            var user = new ApplicationUser
            {
                Id = userId,
                FirstName = uniqueSuffix,
                LastName = uniqueSuffix,
                UserName = $"User_{uniqueSuffix}",
                NormalizedUserName = $"USER_{uniqueSuffix}",
                Email = $"u_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"U_{uniqueSuffix}@T.PL"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"DealCompany_{uniqueSuffix}",
                NIP = "999",
                OwnerId = userId,
                Owner = user
            };

            var address = new CompanyAdress
            {
                CompanyId = company.Id,
                Company = company,
                City = "X",
                Street = "Y",
                ZipCode = "Z",
                AddressType = AddressTypeEnum.Headquarters
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "PLN",
                Code = "PLN",
            };

            var oldDealDate = DateTime.UtcNow.AddDays(-10);
            var newDealDate = DateTime.UtcNow.AddDays(-1);

            var oldDeal = new Deal
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Company = company,
                CreatedAt = oldDealDate,
                Name = "Old",
                OwnerId = userId,
                Owner = user,
                CurrencyId = currency.Id,
                Currency = currency
            };

            var newDeal = new Deal
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Company = company,
                CreatedAt = newDealDate,
                Name = "New",
                OwnerId = userId,
                Owner = user,
                CurrencyId = currency.Id,
                Currency = currency
            };

            _contextMock.Users.Add(user);
            _contextMock.Companies.Add(company);
            _contextMock.CompanyAdresses.Add(address);
            _contextMock.Currencies.Add(currency);
            _contextMock.Deals.AddRange(oldDeal, newDeal);

            await _contextMock.SaveChangesAsync();

            oldDeal.CreatedAt = oldDealDate;
            newDeal.CreatedAt = newDealDate;
            _contextMock.Deals.UpdateRange(oldDeal, newDeal);
            await _contextMock.SaveChangesAsync();

            var command = new CompanyListCommand
            {
                PageNumber = 1,
                PageSize = 10,
                IsYour = true,
                UserId = userId
            };

            // Act
            var result = await _companyServicesMock.GetCompanyListAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            var returnedCompany = result.Data!.Items.FirstOrDefault(c => c.Id == company.Id);

            await Assert.That(returnedCompany).IsNotNull();

            await Assert.That(returnedCompany!.LastDealDate).IsNotNull();

            var difference = returnedCompany.LastDealDate!.Value - newDealDate;
            await Assert.That(Math.Abs(difference.TotalSeconds) < 1).IsTrue();
        }

        [Test]
        public async Task GetCompanyListAsync_AppliesSearchAndSortingCorrectly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userId = Guid.NewGuid();
            var user = new ApplicationUser
            {
                Id = userId,
                FirstName = uniqueSuffix,
                LastName = uniqueSuffix,
                UserName = $"Searcher_{uniqueSuffix}",
                Email = $"s_{uniqueSuffix}@t.pl"
            };

            var companyA = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Apple_{uniqueSuffix}",
                NIP = "1000",
                OwnerId = userId,
                Owner = user
            };

            var addressA = new CompanyAdress
            {
                CompanyId = companyA.Id,
                Company = companyA,
                City = "Kraków",
                Street = "Długa",
                ZipCode = "1",
                AddressType = AddressTypeEnum.Headquarters
            };

            var companyB = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Banana_{uniqueSuffix}",
                NIP = "2000",
                OwnerId = userId,
                Owner = user
            };

            var addressB = new CompanyAdress
            {
                CompanyId = companyB.Id,
                Company = companyB,
                City = "Warszawa",
                Street = "Krótka",
                ZipCode = "2",
                AddressType = AddressTypeEnum.Headquarters
            };

            _contextMock.Users.Add(user);
            _contextMock.Companies.AddRange(companyA, companyB);
            _contextMock.CompanyAdresses.AddRange(addressA, addressB);
            await _contextMock.SaveChangesAsync();

            var command = new CompanyListCommand
            {
                PageNumber = 1,
                PageSize = 10,
                UserId = userId,
                SearchTerm = "apple",
                SortBy = "name",
                SortDescending = true
            };

            // Act
            var result = await _companyServicesMock.GetCompanyListAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();

            var items = result.Data!.Items;
            await Assert.That(items.Any(c => c.Name == companyA.Name)).IsTrue();
            await Assert.That(items.Any(c => c.Name == companyB.Name)).IsFalse();
        }

        [Test]
        public async Task GetCompanyListAsync_IgnoresDeletedCompaniesAndAppliesDateFilters()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userId = Guid.NewGuid();
            var user = new ApplicationUser
            {
                Id = userId,
                FirstName = $"FilterFirst_{uniqueSuffix}",
                LastName = $"FilterLast_{uniqueSuffix}",
                UserName = $"FilterUser_{uniqueSuffix}",
                Email = $"f_{uniqueSuffix}@test.pl"
            };

            var now = DateTime.UtcNow;
            var referenceDate = now;

            var deletedCompany = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Deleted_{uniqueSuffix}",
                NIP = "111",
                OwnerId = userId,
                Owner = user,
                IsDeleted = true,
                CreatedAt = referenceDate
            };

            var deletedAddress = new CompanyAdress
            {
                CompanyId = deletedCompany.Id,
                Company = deletedCompany,
                City = "X",
                Street = "Y",
                ZipCode = "Z",
                AddressType = AddressTypeEnum.Headquarters
            };

            var oldCompany = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Old_{uniqueSuffix}",
                NIP = "222",
                OwnerId = userId,
                Owner = user,
                IsDeleted = false,
                CreatedAt = referenceDate.AddDays(-10)
            };

            var oldAddress = new CompanyAdress
            {
                CompanyId = oldCompany.Id,
                Company = oldCompany,
                City = "X",
                Street = "Y",
                ZipCode = "Z",
                AddressType = AddressTypeEnum.Headquarters
            };

            var validCompany = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Valid_{uniqueSuffix}",
                NIP = "333",
                OwnerId = userId,
                Owner = user,
                IsDeleted = false,
                CreatedAt = referenceDate
            };

            var validAddress = new CompanyAdress
            {
                CompanyId = validCompany.Id,
                Company = validCompany,
                City = "X",
                Street = "Y",
                ZipCode = "Z",
                AddressType = AddressTypeEnum.Headquarters
            };

            _contextMock.Users.Add(user);
            _contextMock.Companies.AddRange(deletedCompany, oldCompany, validCompany);
            _contextMock.CompanyAdresses.AddRange(deletedAddress, oldAddress, validAddress);
            await _contextMock.SaveChangesAsync();

            oldCompany.CreatedAt = referenceDate.AddDays(-10);
            _contextMock.Companies.Update(oldCompany);
            await _contextMock.SaveChangesAsync();

            var command = new CompanyListCommand
            {
                PageNumber = 1,
                PageSize = 10,
                CreatedAtFrom = referenceDate.AddMinutes(-1),
                CreatedAtTo = referenceDate.AddMinutes(1),
                UserId = userId
            };

            // Act
            var result = await _companyServicesMock.GetCompanyListAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var items = result.Data!.Items;

            await Assert.That(items.Any(c => c.Id == deletedCompany.Id)).IsFalse();
            await Assert.That(items.Any(c => c.Id == oldCompany.Id)).IsFalse();
            await Assert.That(items.Any(c => c.Id == validCompany.Id)).IsTrue();
        }

        [Test]
        public async Task GetCompanyListAsync_WhenMultiWordSearchTermProvided_FindsCompanyCorrectly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"Owner_{uniqueSuffix}",
                NormalizedUserName = $"OWNER_{uniqueSuffix}",
                Email = $"owner_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"owner_{uniqueSuffix}@TEST.PL",
                FirstName = "Grzegorz",
                LastName = "Brzęczyszczykiewicz",
                EmailConfirmed = true
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"StalGuard",
                NIP = "1234567890",
                OwnerId = ownerId,
                Owner = owner
            };

            var address = new CompanyAdress
            {
                CompanyId = company.Id,
                Company = company,
                City = "Chrząszczyzewoszyce",
                Street = "Polna 1",
                ZipCode = "00-001",
                AddressType = AddressTypeEnum.Headquarters
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.CompanyAdresses.Add(address);
            await _contextMock.SaveChangesAsync();

            var command = new CompanyListCommand
            {
                PageNumber = 1,
                PageSize = 10,
                UserId = Guid.NewGuid(),
                SearchTerm = $"Brzęczyszczykiewicz StalGuard"
            };

            // Act
            var result = await _companyServicesMock.GetCompanyListAsync(command);

            if (result.IsSuccess)
            {
                Console.WriteLine(result);
            }

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var foundCompany = result.Data!.Items.FirstOrDefault(c => c.Id == company.Id);

            await Assert.That(foundCompany).IsNotNull();
            await Assert.That(foundCompany!.Name).IsEqualTo(company.Name);
        }

        // ─── GetCompanySimpleListAsync ──────────────────────────────────────────

        [Test]
        public async Task GetCompanySimpleListAsync_ReturnsAllCompanies_SortedByNameAlphabetically()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var owner = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"User_{uniqueSuffix}",
                Email = $"user_{uniqueSuffix}@test.pl",
                FirstName = "Jan",
                LastName = "Kowalski"
            };

            var companyZ = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Zeta Stal_{uniqueSuffix}",
                NIP = "1111111111",
                OwnerId = owner.Id,
                Owner = owner,
                IsDeleted = false
            };

            var companyA = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Alfa Met_{uniqueSuffix}",
                NIP = "2222222222",
                OwnerId = owner.Id,
                Owner = owner,
                IsDeleted = false
            };

            var companyM = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Mega Hut_{uniqueSuffix}",
                NIP = "3333333333",
                OwnerId = owner.Id,
                Owner = owner,
                IsDeleted = false
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.AddRange(companyZ, companyA, companyM);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _companyServicesMock.GetCompanySimpleListAsync();

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();

            var relevantCompanies = result.Data!
                .Where(c => c.Name.EndsWith(uniqueSuffix))
                .ToList();

            await Assert.That(relevantCompanies.Count).IsEqualTo(3);
            await Assert.That(relevantCompanies[0].Name).IsEqualTo(companyA.Name);
            await Assert.That(relevantCompanies[1].Name).IsEqualTo(companyM.Name);
            await Assert.That(relevantCompanies[2].Name).IsEqualTo(companyZ.Name);
            await Assert.That(relevantCompanies[0].Id).IsEqualTo(companyA.Id);
        }

        [Test]
        public async Task GetCompanySimpleListAsync_IgnoresSoftDeletedCompanies()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var owner = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"User_{uniqueSuffix}",
                Email = $"user_{uniqueSuffix}@test.pl",
                FirstName = "Jan",
                LastName = "Kowalski"
            };

            var activeCompany = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Active Company_{uniqueSuffix}",
                NIP = "1234567890",
                OwnerId = owner.Id,
                Owner = owner,
                IsDeleted = false
            };

            var deletedCompany = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Deleted Company_{uniqueSuffix}",
                NIP = "0987654321",
                OwnerId = owner.Id,
                Owner = owner,
                IsDeleted = true
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.AddRange(activeCompany, deletedCompany);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _companyServicesMock.GetCompanySimpleListAsync();

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var relevantCompanies = result.Data!
                .Where(c => c.Name.EndsWith(uniqueSuffix))
                .ToList();

            await Assert.That(relevantCompanies.Count).IsEqualTo(1);
            await Assert.That(relevantCompanies.First().Id).IsEqualTo(activeCompany.Id);
            await Assert.That(relevantCompanies.First().Name).IsEqualTo(activeCompany.Name);
        }

        [Test]
        public async Task GetCompanySimpleListAsync_ReturnsEmptyList_WhenNoCompaniesExist()
        {
            // Act
            var result = await _companyServicesMock.GetCompanySimpleListAsync();

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.Count).IsEqualTo(0);
        }

        // ─── AddCompanyAsync ─────────────────────────────────────────────────

        [Test]
        public async Task AddCompanyAsync_WhenDataIsValid_CreatesCompanyWithAddressesAndReturnsCreated()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = userId,
                UserName = $"Owner_{uniqueSuffix}",
                NormalizedUserName = $"OWNER_{uniqueSuffix}",
                Email = $"owner_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"OWNER_{uniqueSuffix}@TEST.PL",
                FirstName = "Jan",
                LastName = "Kowalski",
                EmailConfirmed = true
            };

            _contextMock.Users.Add(owner);
            await _contextMock.SaveChangesAsync();

            var command = new AddCompanyCommand
            {
                Name = $"StalNowa_{uniqueSuffix}",
                NIP = "5252344078",
                Adresses = new List<AddCompanyAdressCommand>
                {
                    new()
                    {
                        Street = "Złota 44",
                        City = "Warszawa",
                        ZipCode = "00-120",
                        Location = new Point(21.0035, 52.2319) { SRID = SRID },
                        Type = AddressTypeEnum.Headquarters
                    },
                    new()
                    {
                        Street = "Hutnicza 15",
                        City = "Katowice",
                        ZipCode = "40-241",
                        Location = new Point(19.0560, 50.2649) { SRID = SRID },
                        Type = AddressTypeEnum.Branch
                    }
                }
            };

            // Act
            var result = await _companyServicesMock.AddCompanyAsync(command, userId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status201Created);
            await Assert.That(result.Data).IsNotEqualTo(Guid.Empty);

            var savedCompany = await _contextMock.Companies
                .Include(c => c.CompanyAdresses)
                .FirstOrDefaultAsync(c => c.Id == result.Data);

            await Assert.That(savedCompany).IsNotNull();
            await Assert.That(savedCompany!.Name).IsEqualTo(command.Name);
            await Assert.That(savedCompany.NIP).IsEqualTo(command.NIP);
            await Assert.That(savedCompany.OwnerId).IsEqualTo(userId);
            await Assert.That(savedCompany.CompanyAdresses.Count).IsEqualTo(2);

            var hqAddress = savedCompany.CompanyAdresses.FirstOrDefault(a => a.AddressType == AddressTypeEnum.Headquarters);
            await Assert.That(hqAddress).IsNotNull();
            await Assert.That(hqAddress!.Street).IsEqualTo("Złota 44");
            await Assert.That(hqAddress.City).IsEqualTo("Warszawa");
            await Assert.That(hqAddress.Location).IsNotNull();
            await Assert.That(hqAddress.Location!.X).IsEqualTo(21.0035);
            await Assert.That(hqAddress.Location.Y).IsEqualTo(52.2319);
        }

        [Test]
        public async Task AddCompanyAsync_WhenCompanyWithSameNameOrNipExists_ReturnsBadRequest()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = userId,
                UserName = $"Owner_{uniqueSuffix}",
                NormalizedUserName = $"OWNER_{uniqueSuffix}",
                Email = $"owner_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"OWNER_{uniqueSuffix}@TEST.PL",
                FirstName = "Jan",
                LastName = "Kowalski",
                EmailConfirmed = true
            };

            var existingCompany = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"ExistingCompany_{uniqueSuffix}",
                NIP = "1234567890",
                OwnerId = userId,
                Owner = owner
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(existingCompany);
            await _contextMock.SaveChangesAsync();

            var commandWithSameName = new AddCompanyCommand
            {
                Name = $"EXISTINGCOMPANY_{uniqueSuffix}",
                NIP = "9998887776",
                Adresses = new List<AddCompanyAdressCommand>
                {
                    new()
                    {
                        Street = "Ulica 1",
                        City = "Kraków",
                        ZipCode = "30-001",
                        Location = new Point(19.9, 50.0) { SRID = SRID },
                        Type = AddressTypeEnum.Headquarters
                    }
                }
            };

            // Przypadek 2: Ten sam NIP
            var commandWithSameNip = new AddCompanyCommand
            {
                Name = $"DifferentName_{uniqueSuffix}",
                NIP = "1234567890",
                Adresses = new List<AddCompanyAdressCommand>
                {
                    new()
                    {
                        Street = "Ulica 1",
                        City = "Kraków",
                        ZipCode = "30-001",
                        Location = new Point(19.9, 50.0) { SRID = SRID },
                        Type = AddressTypeEnum.Headquarters
                    }
                }
            };

            // Act
            var resultName = await _companyServicesMock.AddCompanyAsync(commandWithSameName, userId);
            var resultNip = await _companyServicesMock.AddCompanyAsync(commandWithSameNip, userId);

            // Assert
            await Assert.That(resultName.IsSuccess).IsFalse();
            await Assert.That(resultName.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(resultName.Message).IsEqualTo("Company with the same name or NIP already exists.");

            await Assert.That(resultNip.IsSuccess).IsFalse();
            await Assert.That(resultNip.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(resultNip.Message).IsEqualTo("Company with the same name or NIP already exists.");
        }

        [Test]
        public async Task AddCompanyAsync_WhenAddressesListIsEmpty_ReturnsBadRequest()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userId = Guid.NewGuid();

            var command = new AddCompanyCommand
            {
                Name = $"NoAddressCompany_{uniqueSuffix}",
                NIP = "5554443332",
                Adresses = new List<AddCompanyAdressCommand>()
            };

            // Act
            var result = await _companyServicesMock.AddCompanyAsync(command, userId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.Message).IsEqualTo("Company must have at least one address.");
        }

        [Test]
        [Arguments(0)]
        [Arguments(2)]
        public async Task AddCompanyAsync_WhenHeadquartersCountIsNotExactlyOne_ReturnsBadRequest(int hqCount)
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userId = Guid.NewGuid();

            var addresses = new List<AddCompanyAdressCommand>();

            if (hqCount == 0)
            {
                addresses.Add(new AddCompanyAdressCommand
                {
                    Street = "Oddziałowa 1",
                    City = "Kraków",
                    ZipCode = "30-001",
                    Location = new Point(19.9, 50.0) { SRID = SRID },
                    Type = AddressTypeEnum.Branch
                });
            }
            else
            {
                addresses.Add(new AddCompanyAdressCommand
                {
                    Street = "Centrala 1",
                    City = "Warszawa",
                    ZipCode = "00-001",
                    Location = new Point(21.0, 52.2) { SRID = SRID },
                    Type = AddressTypeEnum.Headquarters
                });
                addresses.Add(new AddCompanyAdressCommand
                {
                    Street = "Centrala 2",
                    City = "Gdańsk",
                    ZipCode = "80-001",
                    Location = new Point(18.6, 54.3) { SRID = SRID },
                    Type = AddressTypeEnum.Headquarters
                });
            }

            var command = new AddCompanyCommand
            {
                Name = $"InvalidHqCompany_{uniqueSuffix}",
                NIP = "7776665554",
                Adresses = addresses
            };

            // Act
            var result = await _companyServicesMock.AddCompanyAsync(command, userId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.Message).IsEqualTo("Company must have exactly one headquarters address.");
        }

        [Test]
        public async Task AddCompanyAsync_WhenCompanyContainsDuplicateAddresses_ReturnsBadRequest()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userId = Guid.NewGuid();

            var command = new AddCompanyCommand
            {
                Name = $"DuplicateAddressCompany_{uniqueSuffix}",
                NIP = "8887776665",
                Adresses = new List<AddCompanyAdressCommand>
        {
            new()
            {
                Street = "Główna 1",
                City = "Poznań",
                ZipCode = "61-001",
                Location = new Point(16.9, 52.4) { SRID = SRID },
                Type = AddressTypeEnum.Headquarters
            },
            new()
            {
                Street = "Długa 10",
                City = "Poznań",
                ZipCode = "61-001",
                Location = new Point(16.9, 52.4) { SRID = SRID },
                Type = AddressTypeEnum.Branch
            },
            new()
            {
                Street = "  długa 10  ",
                City = "POZNAŃ",
                ZipCode = "61-001",
                Location = new Point(16.9, 52.4) { SRID = SRID },
                Type = AddressTypeEnum.Branch
            }
        }
            };

            // Act
            var result = await _companyServicesMock.AddCompanyAsync(command, userId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.Message).IsEqualTo("Cannot add duplicate addresses for the same company.");
        }

        // ─── EditCompanyAsync ─────────────────────────────────────────────────

        [Test]
        public async Task EditCompanyAsync_WhenCompanyNotFound_Returns404NotFound()
        {
            // Arrange
            var randomCompanyId = Guid.NewGuid();
            var randomUserId = Guid.NewGuid();

            var command = new EditCompanyCommand
            {
                Id = randomCompanyId,
                Name = "Nowa Nazwa",
                NIP = "1234567890"
            };

            // Act
            var result = await _companyServicesMock.EditCompanyAsync(command, randomUserId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.Message).IsEqualTo("Company not found.");
        }

        [Test]
        public async Task EditCompanyAsync_WhenUserIsNotOwnerNorManager_Returns403Forbidden()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();
            var unauthorizedUserId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"Owner_{uniqueSuffix}",
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = $"owner_{uniqueSuffix}@test.pl"
            };

            var unauthorizedUser = new ApplicationUser
            {
                Id = unauthorizedUserId,
                UserName = $"Unauthorized_{uniqueSuffix}",
                FirstName = "Piotr",
                LastName = "Zieliński",
                Email = $"unauth_{uniqueSuffix}@test.pl"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Company_{uniqueSuffix}",
                NIP = "1112223334",
                OwnerId = ownerId,
                Owner = owner
            };

            _contextMock.Users.AddRange(owner, unauthorizedUser);
            _contextMock.Companies.Add(company);
            await _contextMock.SaveChangesAsync();

            var command = new EditCompanyCommand
            {
                Id = company.Id,
                Name = $"NewName_{uniqueSuffix}",
                NIP = "9998887776"
            };

            // Act
            var result = await _companyServicesMock.EditCompanyAsync(command, unauthorizedUserId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status403Forbidden);
            await Assert.That(result.Message).IsEqualTo("You are not authorized to modify this company.");
        }

        [Test]
        public async Task EditCompanyAsync_WhenOwnerEditsData_UpdatesNameAndNipSuccessfully()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"Owner_{uniqueSuffix}",
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = $"owner_{uniqueSuffix}@test.pl"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"OldName_{uniqueSuffix}",
                NIP = "1112223334",
                OwnerId = ownerId,
                Owner = owner
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            await _contextMock.SaveChangesAsync();

            _contextMock.ChangeTracker.Clear();

            var command = new EditCompanyCommand
            {
                Id = company.Id,
                Name = $"  Updated Name_{uniqueSuffix}  ",
                NIP = " 999-888-77-66 "
            };

            // Act
            var result = await _companyServicesMock.EditCompanyAsync(command, ownerId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("Company updated successfully.");

            var updatedCompany = await _contextMock.Companies.FindAsync(company.Id);
            await Assert.That(updatedCompany).IsNotNull();
            await Assert.That(updatedCompany!.Name).IsEqualTo($"Updated Name_{uniqueSuffix}");
            await Assert.That(updatedCompany.NIP).IsEqualTo("9998887766");
        }

        [Test]
        public async Task EditCompanyAsync_WhenUserIsManager_AllowsEditEvenIfNotOwner()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();
            var managerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"Owner_{uniqueSuffix}",
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = $"owner_{uniqueSuffix}@test.pl"
            };

            var manager = new ApplicationUser
            {
                Id = managerId,
                UserName = $"Manager_{uniqueSuffix}",
                FirstName = "Adam",
                LastName = "Nowak",
                Email = $"manager_{uniqueSuffix}@test.pl"
            };

            var managerRole = new IdentityRole<Guid>
            {
                Id = Guid.NewGuid(),
                Name = "Manager",
                NormalizedName = "MANAGER"
            };

            var userRole = new IdentityUserRole<Guid>
            {
                UserId = managerId,
                RoleId = managerRole.Id
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"OldCompany_{uniqueSuffix}",
                NIP = "1231231234",
                OwnerId = ownerId,
                Owner = owner
            };

            _contextMock.Users.AddRange(owner, manager);
            _contextMock.Roles.Add(managerRole);
            _contextMock.UserRoles.Add(userRole);
            _contextMock.Companies.Add(company);
            await _contextMock.SaveChangesAsync();

            _contextMock.ChangeTracker.Clear();

            var command = new EditCompanyCommand
            {
                Id = company.Id,
                Name = $"EditedByManager_{uniqueSuffix}",
                NIP = "5556667778"
            };

            // Act
            var result = await _companyServicesMock.EditCompanyAsync(command, managerId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var updatedCompany = await _contextMock.Companies.FindAsync(company.Id);
            await Assert.That(updatedCompany).IsNotNull();
            await Assert.That(updatedCompany!.Name).IsEqualTo($"EditedByManager_{uniqueSuffix}");
            await Assert.That(updatedCompany.NIP).IsEqualTo("5556667778");
        }

        [Test]
        public async Task EditCompanyAsync_WhenAnotherCompanyHasSameName_Returns400BadRequest()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"Owner_{uniqueSuffix}",
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = $"owner_{uniqueSuffix}@test.pl"
            };

            var otherCompany = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"ExistingName_{uniqueSuffix}",
                NIP = "1111111111",
                OwnerId = ownerId,
                Owner = owner
            };

            var targetCompany = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"TargetName_{uniqueSuffix}",
                NIP = "2222222222",
                OwnerId = ownerId,
                Owner = owner
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.AddRange(otherCompany, targetCompany);
            await _contextMock.SaveChangesAsync();

            var command = new EditCompanyCommand
            {
                Id = targetCompany.Id,
                Name = $"EXISTINGNAME_{uniqueSuffix}",
                NIP = "3333333333"
            };

            // Act
            var result = await _companyServicesMock.EditCompanyAsync(command, ownerId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.Message).IsEqualTo("Company with the same name already exists.");
        }

        [Test]
        public async Task EditCompanyAsync_WhenAnotherCompanyHasSameNip_Returns400BadRequest()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"Owner_{uniqueSuffix}",
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = $"owner_{uniqueSuffix}@test.pl"
            };

            var otherCompany = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"OtherCompany_{uniqueSuffix}",
                NIP = "5252344078",
                OwnerId = ownerId,
                Owner = owner
            };

            var targetCompany = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"TargetCompany_{uniqueSuffix}",
                NIP = "9999999999",
                OwnerId = ownerId,
                Owner = owner
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.AddRange(otherCompany, targetCompany);
            await _contextMock.SaveChangesAsync();

            var command = new EditCompanyCommand
            {
                Id = targetCompany.Id,
                Name = $"UniqueName_{uniqueSuffix}",
                NIP = "525-234-40-78"
            };

            // Act
            var result = await _companyServicesMock.EditCompanyAsync(command, ownerId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.Message).IsEqualTo("Company with the same NIP already exists.");
        }

        [Test]
        public async Task EditCompanyAsync_WhenKeepingSameNameAndNipForSameCompany_DoesNotConflictWithItself()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"Owner_{uniqueSuffix}",
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = $"owner_{uniqueSuffix}@test.pl"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"StableName_{uniqueSuffix}",
                NIP = "1234567890",
                OwnerId = ownerId,
                Owner = owner
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            await _contextMock.SaveChangesAsync();

            var command = new EditCompanyCommand
            {
                Id = company.Id,
                Name = company.Name,
                NIP = "123-456-78-90"
            };

            // Act
            var result = await _companyServicesMock.EditCompanyAsync(command, ownerId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("Company updated successfully.");
        }

        // ─── EditCompanyAddressAsync ─────────────────────────────────────────

        [Test]
        public async Task EditCompanyAddressAsync_WhenAddressNotFound_Returns404NotFound()
        {
            // Arrange
            var randomAddressId = Guid.NewGuid();
            var randomUserId = Guid.NewGuid();

            var command = new EditCompanyAddressCommand
            {
                AddressId = randomAddressId,
                Street = "Nowa Ulica 1"
            };

            // Act
            var result = await _companyServicesMock.EditCompanyAddressAsync(command, randomUserId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.Message).IsEqualTo("Address not found.");
        }

        [Test]
        public async Task EditCompanyAddressAsync_WhenUserIsNotOwnerNorManager_Returns403Forbidden()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();
            var unauthorizedUserId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"Owner_{uniqueSuffix}",
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = $"owner_{uniqueSuffix}@test.pl"
            };

            var unauthorizedUser = new ApplicationUser
            {
                Id = unauthorizedUserId,
                UserName = $"Unauth_{uniqueSuffix}",
                FirstName = "Piotr",
                LastName = "Nowak",
                Email = $"unauth_{uniqueSuffix}@test.pl"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Company_{uniqueSuffix}",
                NIP = "1112223334",
                OwnerId = ownerId,
                Owner = owner
            };

            var address = new CompanyAdress
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Company = company,
                Street = "Stara 1",
                City = "Warszawa",
                ZipCode = "00-001",
                AddressType = AddressTypeEnum.Headquarters,
                Location = new Point(21.0, 52.2) { SRID = SRID }
            };

            _contextMock.Users.AddRange(owner, unauthorizedUser);
            _contextMock.Companies.Add(company);
            _contextMock.CompanyAdresses.Add(address);
            await _contextMock.SaveChangesAsync();

            var command = new EditCompanyAddressCommand
            {
                AddressId = address.Id,
                Street = "Zmieniona 10"
            };

            // Act
            var result = await _companyServicesMock.EditCompanyAddressAsync(command, unauthorizedUserId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status403Forbidden);
            await Assert.That(result.Message).IsEqualTo("You are not authorized to modify this address.");
        }

        [Test]
        public async Task EditCompanyAddressAsync_WhenOwnerEditsFields_UpdatesDetailsAndCoordinates()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"Owner_{uniqueSuffix}",
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = $"owner_{uniqueSuffix}@test.pl"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Company_{uniqueSuffix}",
                NIP = "1112223334",
                OwnerId = ownerId,
                Owner = owner
            };

            var address = new CompanyAdress
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Company = company,
                Street = "Stara 1",
                City = "Poznań",
                ZipCode = "60-001",
                AddressType = AddressTypeEnum.Headquarters,
                Location = new Point(16.9, 52.4) { SRID = SRID }
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.CompanyAdresses.Add(address);
            await _contextMock.SaveChangesAsync();

            _contextMock.ChangeTracker.Clear();

            var newPoint = new Point(19.9, 50.0) { SRID = SRID };
            var command = new EditCompanyAddressCommand
            {
                AddressId = address.Id,
                Street = "  Nowa Długa 10  ",
                City = "  Kraków  ",
                ZipCode = "  30-001  ",
                Location = newPoint
            };

            // Act
            var result = await _companyServicesMock.EditCompanyAddressAsync(command, ownerId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("Address updated successfully.");

            var updatedAddress = await _contextMock.CompanyAdresses.FindAsync(address.Id);
            await Assert.That(updatedAddress).IsNotNull();
            await Assert.That(updatedAddress!.Street).IsEqualTo("Nowa Długa 10");
            await Assert.That(updatedAddress.City).IsEqualTo("Kraków");
            await Assert.That(updatedAddress.ZipCode).IsEqualTo("30-001");
            await Assert.That(updatedAddress.Location).IsNotNull();
            await Assert.That(updatedAddress.Location!.X).IsEqualTo(19.9);
            await Assert.That(updatedAddress.Location.Y).IsEqualTo(50.0);
        }

        [Test]
        public async Task EditCompanyAddressAsync_WhenBranchPromotedToHeadquarters_DemotesOldHeadquartersToBranch()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"Owner_{uniqueSuffix}",
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = $"owner_{uniqueSuffix}@test.pl"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Company_{uniqueSuffix}",
                NIP = "1112223334",
                OwnerId = ownerId,
                Owner = owner
            };

            var oldHq = new CompanyAdress
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Company = company,
                Street = "Centrala 1",
                City = "Warszawa",
                ZipCode = "00-001",
                AddressType = AddressTypeEnum.Headquarters,
                Location = new Point(21.0, 52.2) { SRID = SRID }
            };

            var branchToPromote = new CompanyAdress
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Company = company,
                Street = "Oddział 1",
                City = "Wrocław",
                ZipCode = "50-001",
                AddressType = AddressTypeEnum.Branch,
                Location = new Point(17.0, 51.1) { SRID = SRID }
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.CompanyAdresses.AddRange(oldHq, branchToPromote);
            await _contextMock.SaveChangesAsync();

            _contextMock.ChangeTracker.Clear();

            var command = new EditCompanyAddressCommand
            {
                AddressId = branchToPromote.Id,
                Type = AddressTypeEnum.Headquarters
            };

            // Act
            var result = await _companyServicesMock.EditCompanyAddressAsync(command, ownerId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var updatedOldHq = await _contextMock.CompanyAdresses.FindAsync(oldHq.Id);
            var updatedPromotedAddress = await _contextMock.CompanyAdresses.FindAsync(branchToPromote.Id);

            await Assert.That(updatedOldHq).IsNotNull();
            await Assert.That(updatedOldHq!.AddressType).IsEqualTo(AddressTypeEnum.Branch);

            await Assert.That(updatedPromotedAddress).IsNotNull();
            await Assert.That(updatedPromotedAddress!.AddressType).IsEqualTo(AddressTypeEnum.Headquarters);
        }

        [Test]
        public async Task EditCompanyAddressAsync_WhenAttemptingToDemoteTheOnlyHeadquarters_Returns400BadRequest()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"Owner_{uniqueSuffix}",
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = $"owner_{uniqueSuffix}@test.pl"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Company_{uniqueSuffix}",
                NIP = "1112223334",
                OwnerId = ownerId,
                Owner = owner
            };

            var onlyHq = new CompanyAdress
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Company = company,
                Street = "Centrala 1",
                City = "Warszawa",
                ZipCode = "00-001",
                AddressType = AddressTypeEnum.Headquarters,
                Location = new Point(21.0, 52.2) { SRID = SRID }
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.CompanyAdresses.Add(onlyHq);
            await _contextMock.SaveChangesAsync();

            _contextMock.ChangeTracker.Clear();

            var command = new EditCompanyAddressCommand
            {
                AddressId = onlyHq.Id,
                Type = AddressTypeEnum.Branch
            };

            // Act
            var result = await _companyServicesMock.EditCompanyAddressAsync(command, ownerId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.Message).Contains("The company must have a headquarters");

            var unmodifiedHq = await _contextMock.CompanyAdresses.FindAsync(onlyHq.Id);
            await Assert.That(unmodifiedHq).IsNotNull();
            await Assert.That(unmodifiedHq!.AddressType).IsEqualTo(AddressTypeEnum.Headquarters);
        }

        [Test]
        public async Task EditCompanyAddressAsync_WhenUserIsManager_AllowsEditEvenIfNotOwner()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();
            var managerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"Owner_{uniqueSuffix}",
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = $"owner_{uniqueSuffix}@test.pl"
            };

            var manager = new ApplicationUser
            {
                Id = managerId,
                UserName = $"Manager_{uniqueSuffix}",
                FirstName = "Adam",
                LastName = "Nowak",
                Email = $"manager_{uniqueSuffix}@test.pl"
            };

            var managerRole = new IdentityRole<Guid>
            {
                Id = Guid.NewGuid(),
                Name = "Manager",
                NormalizedName = "MANAGER"
            };

            var userRole = new IdentityUserRole<Guid>
            {
                UserId = managerId,
                RoleId = managerRole.Id
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Company_{uniqueSuffix}",
                NIP = "1112223334",
                OwnerId = ownerId,
                Owner = owner
            };

            var address = new CompanyAdress
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Company = company,
                Street = "Stara 1",
                City = "Gdańsk",
                ZipCode = "80-001",
                AddressType = AddressTypeEnum.Headquarters,
                Location = new Point(18.6, 54.3) { SRID = SRID }
            };

            _contextMock.Users.AddRange(owner, manager);
            _contextMock.Roles.Add(managerRole);
            _contextMock.UserRoles.Add(userRole);
            _contextMock.Companies.Add(company);
            _contextMock.CompanyAdresses.Add(address);
            await _contextMock.SaveChangesAsync();

            _contextMock.ChangeTracker.Clear();

            var command = new EditCompanyAddressCommand
            {
                AddressId = address.Id,
                Street = "Modyfikacja Managera"
            };

            // Act
            var result = await _companyServicesMock.EditCompanyAddressAsync(command, managerId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var updatedAddress = await _contextMock.CompanyAdresses.FindAsync(address.Id);
            await Assert.That(updatedAddress).IsNotNull();
            await Assert.That(updatedAddress!.Street).IsEqualTo("Modyfikacja Managera");
        }

        // ─── AddCompanyAddressAsync ──────────────────────────────────────────

        [Test]
        public async Task AddCompanyAddressAsync_WhenCompanyNotFound_Returns404NotFound()
        {
            // Arrange
            var randomCompanyId = Guid.NewGuid();
            var randomUserId = Guid.NewGuid();

            var command = new AddCompanyAdressCommand
            {
                Street = "Ulica 1",
                City = "Warszawa",
                ZipCode = "00-001",
                Location = new Point(21.0, 52.2) { SRID = SRID },
                Type = AddressTypeEnum.Branch
            };

            // Act
            var result = await _companyServicesMock.AddCompanyAddressAsync(command, randomUserId, randomCompanyId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.Message).IsEqualTo("Company not found.");
        }

        [Test]
        public async Task AddCompanyAddressAsync_WhenUserIsNotOwnerNorManager_Returns403Forbidden()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();
            var unauthorizedUserId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"Owner_{uniqueSuffix}",
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = $"owner_{uniqueSuffix}@test.pl"
            };

            var unauthorizedUser = new ApplicationUser
            {
                Id = unauthorizedUserId,
                UserName = $"Unauth_{uniqueSuffix}",
                FirstName = "Piotr",
                LastName = "Nowak",
                Email = $"unauth_{uniqueSuffix}@test.pl"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Company_{uniqueSuffix}",
                NIP = "1112223334",
                OwnerId = ownerId,
                Owner = owner
            };

            _contextMock.Users.AddRange(owner, unauthorizedUser);
            _contextMock.Companies.Add(company);
            await _contextMock.SaveChangesAsync();

            var command = new AddCompanyAdressCommand
            {
                Street = "Nowa 1",
                City = "Kraków",
                ZipCode = "30-001",
                Location = new Point(19.9, 50.0) { SRID = SRID },
                Type = AddressTypeEnum.Branch
            };

            // Act
            var result = await _companyServicesMock.AddCompanyAddressAsync(command, unauthorizedUserId, company.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status403Forbidden);
            await Assert.That(result.Message).IsEqualTo("You are not authorized to modify this company.");
        }

        [Test]
        public async Task AddCompanyAddressAsync_WhenValidBranchAddressProvided_AddsAddressAndReturns201Created()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"Owner_{uniqueSuffix}",
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = $"owner_{uniqueSuffix}@test.pl"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Company_{uniqueSuffix}",
                NIP = "1112223334",
                OwnerId = ownerId,
                Owner = owner
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            await _contextMock.SaveChangesAsync();

            _contextMock.ChangeTracker.Clear();

            var location = new Point(19.9449, 50.0646) { SRID = SRID };
            var command = new AddCompanyAdressCommand
            {
                Street = "  Floriańska 10  ",
                City = "  Kraków  ",
                ZipCode = "  30-001  ",
                Location = location,
                Type = AddressTypeEnum.Branch
            };

            // Act
            var result = await _companyServicesMock.AddCompanyAddressAsync(command, ownerId, company.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status201Created);
            await Assert.That(result.Message).IsEqualTo("Address added successfully.");
            await Assert.That(result.Data).IsNotEqualTo(Guid.Empty);

            var savedAddress = await _contextMock.CompanyAdresses.FindAsync(result.Data);
            await Assert.That(savedAddress).IsNotNull();
            await Assert.That(savedAddress!.CompanyId).IsEqualTo(company.Id);
            await Assert.That(savedAddress.Street).IsEqualTo("Floriańska 10");
            await Assert.That(savedAddress.City).IsEqualTo("Kraków");
            await Assert.That(savedAddress.ZipCode).IsEqualTo("30-001");
            await Assert.That(savedAddress.AddressType).IsEqualTo(AddressTypeEnum.Branch);
            await Assert.That(savedAddress.Location).IsNotNull();
            await Assert.That(savedAddress.Location!.X).IsEqualTo(19.9449);
            await Assert.That(savedAddress.Location.Y).IsEqualTo(50.0646);
        }

        [Test]
        public async Task AddCompanyAddressAsync_WhenDuplicateAddressProvidedForSameCompany_Returns400BadRequest()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"Owner_{uniqueSuffix}",
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = $"owner_{uniqueSuffix}@test.pl"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Company_{uniqueSuffix}",
                NIP = "1112223334",
                OwnerId = ownerId,
                Owner = owner
            };

            var existingAddress = new CompanyAdress
            {
                CompanyId = company.Id,
                Company = company,
                Street = "Długa 5",
                City = "Wrocław",
                ZipCode = "50-001",
                AddressType = AddressTypeEnum.Branch,
                Location = new Point(17.0, 51.1) { SRID = SRID }
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.CompanyAdresses.Add(existingAddress);
            await _contextMock.SaveChangesAsync();

            var command = new AddCompanyAdressCommand
            {
                Street = "  długa 5  ",
                City = "WROCŁAW",
                ZipCode = "50-001",
                Location = new Point(17.0, 51.1) { SRID = SRID },
                Type = AddressTypeEnum.Branch
            };

            // Act
            var result = await _companyServicesMock.AddCompanyAddressAsync(command, ownerId, company.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.Message).IsEqualTo("This address already exists for this company.");
        }

        [Test]
        public async Task AddCompanyAddressAsync_WhenNewAddressIsHeadquarters_DemotesExistingHeadquartersToBranch()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"Owner_{uniqueSuffix}",
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = $"owner_{uniqueSuffix}@test.pl"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Company_{uniqueSuffix}",
                NIP = "1112223334",
                OwnerId = ownerId,
                Owner = owner
            };

            var oldHq = new CompanyAdress
            {
                CompanyId = company.Id,
                Company = company,
                Street = "Stara Centrala 1",
                City = "Warszawa",
                ZipCode = "00-001",
                AddressType = AddressTypeEnum.Headquarters,
                Location = new Point(21.0, 52.2) { SRID = SRID }
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.CompanyAdresses.Add(oldHq);
            await _contextMock.SaveChangesAsync();

            _contextMock.ChangeTracker.Clear();

            var command = new AddCompanyAdressCommand
            {
                Street = "Nowa Centrala 10",
                City = "Gdańsk",
                ZipCode = "80-001",
                Location = new Point(18.6, 54.3) { SRID = SRID },
                Type = AddressTypeEnum.Headquarters
            };

            // Act
            var result = await _companyServicesMock.AddCompanyAddressAsync(command, ownerId, company.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status201Created);

            var updatedOldHq = await _contextMock.CompanyAdresses.FindAsync(oldHq.Id);
            var newlyAddedHq = await _contextMock.CompanyAdresses.FindAsync(result.Data);

            await Assert.That(updatedOldHq).IsNotNull();
            await Assert.That(updatedOldHq!.AddressType).IsEqualTo(AddressTypeEnum.Branch);

            await Assert.That(newlyAddedHq).IsNotNull();
            await Assert.That(newlyAddedHq!.AddressType).IsEqualTo(AddressTypeEnum.Headquarters);
        }

        [Test]
        public async Task AddCompanyAddressAsync_WhenUserIsManager_AllowsAddingAddressEvenIfNotOwner()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();
            var managerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"Owner_{uniqueSuffix}",
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = $"owner_{uniqueSuffix}@test.pl"
            };

            var manager = new ApplicationUser
            {
                Id = managerId,
                UserName = $"Manager_{uniqueSuffix}",
                FirstName = "Adam",
                LastName = "Nowak",
                Email = $"manager_{uniqueSuffix}@test.pl"
            };

            var managerRole = new IdentityRole<Guid>
            {
                Id = Guid.NewGuid(),
                Name = "Manager",
                NormalizedName = "MANAGER"
            };

            var userRole = new IdentityUserRole<Guid>
            {
                UserId = managerId,
                RoleId = managerRole.Id
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Company_{uniqueSuffix}",
                NIP = "1112223334",
                OwnerId = ownerId,
                Owner = owner
            };

            _contextMock.Users.AddRange(owner, manager);
            _contextMock.Roles.Add(managerRole);
            _contextMock.UserRoles.Add(userRole);
            _contextMock.Companies.Add(company);
            await _contextMock.SaveChangesAsync();

            _contextMock.ChangeTracker.Clear();

            var command = new AddCompanyAdressCommand
            {
                Street = "Oddział Managera 1",
                City = "Poznań",
                ZipCode = "60-001",
                Location = new Point(16.9, 52.4) { SRID = SRID },
                Type = AddressTypeEnum.Branch
            };

            // Act
            var result = await _companyServicesMock.AddCompanyAddressAsync(command, managerId, company.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status201Created);

            var createdAddress = await _contextMock.CompanyAdresses.FindAsync(result.Data);
            await Assert.That(createdAddress).IsNotNull();
            await Assert.That(createdAddress!.Street).IsEqualTo("Oddział Managera 1");
        }

        // ─── DeleteCompanyAsync ──────────────────────────────────────────────

        [Test]
        public async Task DeleteCompanyAsync_WhenCompanyNotFound_Returns404NotFound()
        {
            // Arrange
            var nonExistentCompanyId = Guid.NewGuid();
            var randomUserId = Guid.NewGuid();

            // Act
            var result = await _companyServicesMock.DeleteCompanyAsync(nonExistentCompanyId, randomUserId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.Message).IsEqualTo("Company not found.");
        }

        [Test]
        public async Task DeleteCompanyAsync_WhenUserIsNotOwnerNorManager_Returns403Forbidden()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();
            var unauthorizedUserId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"Owner_{uniqueSuffix}",
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = $"owner_{uniqueSuffix}@test.pl"
            };

            var unauthorizedUser = new ApplicationUser
            {
                Id = unauthorizedUserId,
                UserName = $"Unauth_{uniqueSuffix}",
                FirstName = "Piotr",
                LastName = "Nowak",
                Email = $"unauth_{uniqueSuffix}@test.pl"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Company_{uniqueSuffix}",
                NIP = "1112223334",
                OwnerId = ownerId,
                Owner = owner
            };

            _contextMock.Users.AddRange(owner, unauthorizedUser);
            _contextMock.Companies.Add(company);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _companyServicesMock.DeleteCompanyAsync(company.Id, unauthorizedUserId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status403Forbidden);
            await Assert.That(result.Message).IsEqualTo("You are not authorized to delete this company.");
        }

        [Test]
        public async Task DeleteCompanyAsync_WhenCompanyHasInvoices_Returns400BadRequestDueToDataIntegrity()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"Owner_{uniqueSuffix}",
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = $"owner_{uniqueSuffix}@test.pl"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"InvoiceCompany_{uniqueSuffix}",
                NIP = "5556667778",
                OwnerId = ownerId,
                Owner = owner
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "PLN",
                Code = "PLN"
            };

            var invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = $"FV/{uniqueSuffix}",
                TotalAmount = 50000,
                PaidAmount = 50000,
                IssueDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(14),
                Currency = currency,
                Company = company
            };

            _contextMock.Users.Add(owner);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            _contextMock.Invoices.Add(invoice);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _companyServicesMock.DeleteCompanyAsync(company.Id, ownerId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.Message).Contains("Nie można usunąć firmy posiadającej historię transakcji lub faktur");

            var unmodifiedCompany = await _contextMock.Companies.FindAsync(company.Id);
            await Assert.That(unmodifiedCompany).IsNotNull();
            await Assert.That(unmodifiedCompany!.IsDeleted).IsFalse();
        }

        [Test]
        public async Task DeleteCompanyAsync_WhenCompanyHasDeals_Returns400BadRequestDueToDataIntegrity()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"Owner_{uniqueSuffix}",
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = $"owner_{uniqueSuffix}@test.pl"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"DealCompany_{uniqueSuffix}",
                NIP = "9998881112",
                OwnerId = ownerId,
                Owner = owner
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "PLN",
                Code = "PLN"
            };

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = $"Deal_{uniqueSuffix}",
                Company = company,
                Owner = owner,
                Currency = currency
            };

            _contextMock.Users.Add(owner);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            _contextMock.Deals.Add(deal);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _companyServicesMock.DeleteCompanyAsync(company.Id, ownerId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.Message).Contains("Nie można usunąć firmy posiadającej historię transakcji lub faktur");

            var unmodifiedCompany = await _contextMock.Companies.FindAsync(company.Id);
            await Assert.That(unmodifiedCompany).IsNotNull();
            await Assert.That(unmodifiedCompany!.IsDeleted).IsFalse();
        }

        [Test]
        public async Task DeleteCompanyAsync_WhenCompanyHasNoDealsNorInvoices_DeletesCompanySuccessfullyViaSoftDelete()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"Owner_{uniqueSuffix}",
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = $"owner_{uniqueSuffix}@test.pl"
            };

            var emptyCompany = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"EmptyCompany_{uniqueSuffix}",
                NIP = "1234560000",
                OwnerId = ownerId,
                Owner = owner
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(emptyCompany);
            await _contextMock.SaveChangesAsync();

            _contextMock.ChangeTracker.Clear();

            // Act
            var result = await _companyServicesMock.DeleteCompanyAsync(emptyCompany.Id, ownerId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("Company deleted successfully.");

            var queryResult = await _contextMock.Companies.FirstOrDefaultAsync(c => c.Id == emptyCompany.Id);
            await Assert.That(queryResult).IsNull();

            var softDeletedCompany = await _contextMock.Companies
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == emptyCompany.Id);

            await Assert.That(softDeletedCompany).IsNotNull();
            await Assert.That(softDeletedCompany!.IsDeleted).IsTrue();
        }

        [Test]
        public async Task DeleteCompanyAsync_WhenUserIsManager_AllowsDeletingEmptyCompanyEvenIfNotOwner()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();
            var managerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"Owner_{uniqueSuffix}",
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = $"owner_{uniqueSuffix}@test.pl"
            };

            var manager = new ApplicationUser
            {
                Id = managerId,
                UserName = $"Manager_{uniqueSuffix}",
                FirstName = "Adam",
                LastName = "Nowak",
                Email = $"manager_{uniqueSuffix}@test.pl"
            };

            var managerRole = new IdentityRole<Guid>
            {
                Id = Guid.NewGuid(),
                Name = "Manager",
                NormalizedName = "MANAGER"
            };

            var userRole = new IdentityUserRole<Guid>
            {
                UserId = managerId,
                RoleId = managerRole.Id
            };

            var emptyCompany = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"ManagerDelete_{uniqueSuffix}",
                NIP = "9876543210",
                OwnerId = ownerId,
                Owner = owner
            };

            _contextMock.Users.AddRange(owner, manager);
            _contextMock.Roles.Add(managerRole);
            _contextMock.UserRoles.Add(userRole);
            _contextMock.Companies.Add(emptyCompany);
            await _contextMock.SaveChangesAsync();

            _contextMock.ChangeTracker.Clear();

            // Act
            var result = await _companyServicesMock.DeleteCompanyAsync(emptyCompany.Id, managerId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var softDeletedCompany = await _contextMock.Companies
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == emptyCompany.Id);

            await Assert.That(softDeletedCompany).IsNotNull();
            await Assert.That(softDeletedCompany!.IsDeleted).IsTrue();
        }

        // ─── DeleteCompanyAddressAsync ───────────────────────────────────────

        [Test]
        public async Task DeleteCompanyAddressAsync_WhenAddressNotFound_Returns404NotFound()
        {
            // Arrange
            var randomAddressId = Guid.NewGuid();
            var randomUserId = Guid.NewGuid();

            // Act
            var result = await _companyServicesMock.DeleteCompanyAddressAsync(randomAddressId, randomUserId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.Message).IsEqualTo("Address not found.");
        }

        [Test]
        public async Task DeleteCompanyAddressAsync_WhenUserIsNotOwnerNorManager_Returns403Forbidden()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();
            var unauthorizedUserId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"Owner_{uniqueSuffix}",
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = $"owner_{uniqueSuffix}@test.pl"
            };

            var unauthorizedUser = new ApplicationUser
            {
                Id = unauthorizedUserId,
                UserName = $"Unauth_{uniqueSuffix}",
                FirstName = "Piotr",
                LastName = "Nowak",
                Email = $"unauth_{uniqueSuffix}@test.pl"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Company_{uniqueSuffix}",
                NIP = "1112223334",
                OwnerId = ownerId,
                Owner = owner
            };

            var address = new CompanyAdress
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Company = company,
                Street = "Oddziałowa 1",
                City = "Kraków",
                ZipCode = "30-001",
                AddressType = AddressTypeEnum.Branch,
                Location = new Point(19.9, 50.0) { SRID = SRID }
            };

            _contextMock.Users.AddRange(owner, unauthorizedUser);
            _contextMock.Companies.Add(company);
            _contextMock.CompanyAdresses.Add(address);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _companyServicesMock.DeleteCompanyAddressAsync(address.Id, unauthorizedUserId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status403Forbidden);
            await Assert.That(result.Message).IsEqualTo("You are not authorized to delete this address.");
        }

        [Test]
        public async Task DeleteCompanyAddressAsync_WhenAddressIsHeadquarters_Returns400BadRequest()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"Owner_{uniqueSuffix}",
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = $"owner_{uniqueSuffix}@test.pl"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Company_{uniqueSuffix}",
                NIP = "1112223334",
                OwnerId = ownerId,
                Owner = owner
            };

            var hqAddress = new CompanyAdress
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Company = company,
                Street = "Centralna 1",
                City = "Warszawa",
                ZipCode = "00-001",
                AddressType = AddressTypeEnum.Headquarters,
                Location = new Point(21.0, 52.2) { SRID = SRID }
            };

            var branchAddress = new CompanyAdress
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Company = company,
                Street = "Oddziałowa 2",
                City = "Poznań",
                ZipCode = "60-001",
                AddressType = AddressTypeEnum.Branch,
                Location = new Point(16.9, 52.4) { SRID = SRID }
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.CompanyAdresses.AddRange(hqAddress, branchAddress);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _companyServicesMock.DeleteCompanyAddressAsync(hqAddress.Id, ownerId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.Message).Contains("Cannot delete the headquarters address. Please designate another address as headquarters before deleting this one.");

            var unmodifiedHq = await _contextMock.CompanyAdresses.FindAsync(hqAddress.Id);
            await Assert.That(unmodifiedHq).IsNotNull();
            await Assert.That(unmodifiedHq!.IsDeleted).IsFalse();
        }

        [Test]
        public async Task DeleteCompanyAddressAsync_WhenAttemptingToDeleteTheLastAddress_Returns400BadRequest()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"Owner_{uniqueSuffix}",
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = $"owner_{uniqueSuffix}@test.pl"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Company_{uniqueSuffix}",
                NIP = "1112223334",
                OwnerId = ownerId,
                Owner = owner
            };

            var onlyAddress = new CompanyAdress
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Company = company,
                Street = "Jedyna 1",
                City = "Gdańsk",
                ZipCode = "80-001",
                AddressType = AddressTypeEnum.Branch,
                Location = new Point(18.6, 54.3) { SRID = SRID }
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.CompanyAdresses.Add(onlyAddress);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _companyServicesMock.DeleteCompanyAddressAsync(onlyAddress.Id, ownerId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.Message).Contains("Firma musi posiadać co najmniej jeden adres");

            var unmodifiedAddress = await _contextMock.CompanyAdresses.FindAsync(onlyAddress.Id);
            await Assert.That(unmodifiedAddress).IsNotNull();
            await Assert.That(unmodifiedAddress!.IsDeleted).IsFalse();
        }

        [Test]
        public async Task DeleteCompanyAddressAsync_WhenBranchAddressDeleted_SoftDeletesAddressSuccessfully()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"Owner_{uniqueSuffix}",
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = $"owner_{uniqueSuffix}@test.pl"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Company_{uniqueSuffix}",
                NIP = "1112223334",
                OwnerId = ownerId,
                Owner = owner
            };

            var hqAddress = new CompanyAdress
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Company = company,
                Street = "Centrala 1",
                City = "Warszawa",
                ZipCode = "00-001",
                AddressType = AddressTypeEnum.Headquarters,
                Location = new Point(21.0, 52.2) { SRID = SRID }
            };

            var branchToDelete = new CompanyAdress
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Company = company,
                Street = "Oddział 1",
                City = "Wrocław",
                ZipCode = "50-001",
                AddressType = AddressTypeEnum.Branch,
                Location = new Point(17.0, 51.1) { SRID = SRID }
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.CompanyAdresses.AddRange(hqAddress, branchToDelete);
            await _contextMock.SaveChangesAsync();

            _contextMock.ChangeTracker.Clear();

            // Act
            var result = await _companyServicesMock.DeleteCompanyAddressAsync(branchToDelete.Id, ownerId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("Address deleted successfully.");

            var queryResult = await _contextMock.CompanyAdresses.FirstOrDefaultAsync(ca => ca.Id == branchToDelete.Id);
            await Assert.That(queryResult).IsNull();

            var softDeletedAddress = await _contextMock.CompanyAdresses
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(ca => ca.Id == branchToDelete.Id);

            await Assert.That(softDeletedAddress).IsNotNull();
            await Assert.That(softDeletedAddress!.IsDeleted).IsTrue();
        }

        [Test]
        public async Task DeleteCompanyAddressAsync_WhenUserIsManager_AllowsDeletingBranchEvenIfNotOwner()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();
            var managerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"Owner_{uniqueSuffix}",
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = $"owner_{uniqueSuffix}@test.pl"
            };

            var manager = new ApplicationUser
            {
                Id = managerId,
                UserName = $"Manager_{uniqueSuffix}",
                FirstName = "Adam",
                LastName = "Nowak",
                Email = $"manager_{uniqueSuffix}@test.pl"
            };

            var managerRole = new IdentityRole<Guid>
            {
                Id = Guid.NewGuid(),
                Name = "Manager",
                NormalizedName = "MANAGER"
            };

            var userRole = new IdentityUserRole<Guid>
            {
                UserId = managerId,
                RoleId = managerRole.Id
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Company_{uniqueSuffix}",
                NIP = "1112223334",
                OwnerId = ownerId,
                Owner = owner
            };

            var hqAddress = new CompanyAdress
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Company = company,
                Street = "Centrala 1",
                City = "Warszawa",
                ZipCode = "00-001",
                AddressType = AddressTypeEnum.Headquarters,
                Location = new Point(21.0, 52.2) { SRID = SRID }
            };

            var branchAddress = new CompanyAdress
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Company = company,
                Street = "Oddział 1",
                City = "Wrocław",
                ZipCode = "50-001",
                AddressType = AddressTypeEnum.Branch,
                Location = new Point(17.0, 51.1) { SRID = SRID }
            };

            _contextMock.Users.AddRange(owner, manager);
            _contextMock.Roles.Add(managerRole);
            _contextMock.UserRoles.Add(userRole);
            _contextMock.Companies.Add(company);
            _contextMock.CompanyAdresses.AddRange(hqAddress, branchAddress);
            await _contextMock.SaveChangesAsync();

            _contextMock.ChangeTracker.Clear();

            // Act
            var result = await _companyServicesMock.DeleteCompanyAddressAsync(branchAddress.Id, managerId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var softDeletedAddress = await _contextMock.CompanyAdresses
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(ca => ca.Id == branchAddress.Id);

            await Assert.That(softDeletedAddress).IsNotNull();
            await Assert.That(softDeletedAddress!.IsDeleted).IsTrue();
        }

        // ─── ChangeCompanyOwnerAsync ─────────────────────────────────────────

        [Test]
        public async Task ChangeCompanyOwnerAsync_WhenCompanyNotFound_Returns404NotFound()
        {
            // Arrange
            var command = new ChangeCompanyOwnerCommand
            {
                CompanyId = Guid.NewGuid(),
                UserId = Guid.NewGuid()
            };

            // Act
            var result = await _companyServicesMock.ChangeCompanyOwnerAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.Message).IsEqualTo("Company not found.");
        }

        [Test]
        public async Task ChangeCompanyOwnerAsync_WhenTargetUserIsAlreadyOwner_Returns400BadRequest()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();

            var currentOwner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"Owner_{uniqueSuffix}",
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = $"owner_{uniqueSuffix}@test.pl"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Company_{uniqueSuffix}",
                NIP = "1112223334",
                OwnerId = ownerId,
                Owner = currentOwner
            };

            _contextMock.Users.Add(currentOwner);
            _contextMock.Companies.Add(company);
            await _contextMock.SaveChangesAsync();

            var command = new ChangeCompanyOwnerCommand
            {
                CompanyId = company.Id,
                UserId = ownerId
            };

            // Act
            var result = await _companyServicesMock.ChangeCompanyOwnerAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.Message).IsEqualTo("This user is already the owner of this company.");
        }

        [Test]
        public async Task ChangeCompanyOwnerAsync_WhenUserNotFound_Returns404NotFound()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();
            var nonExistentUserId = Guid.NewGuid();

            var currentOwner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"Owner_{uniqueSuffix}",
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = $"owner_{uniqueSuffix}@test.pl"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Company_{uniqueSuffix}",
                NIP = "1112223334",
                OwnerId = ownerId,
                Owner = currentOwner
            };

            _contextMock.Users.Add(currentOwner);
            _contextMock.Companies.Add(company);
            await _contextMock.SaveChangesAsync();

            var command = new ChangeCompanyOwnerCommand
            {
                CompanyId = company.Id,
                UserId = nonExistentUserId
            };

            // Act
            var result = await _companyServicesMock.ChangeCompanyOwnerAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.Message).IsEqualTo("User not found.");
        }

        [Test]
        public async Task ChangeCompanyOwnerAsync_WhenTargetUserIsAdmin_Returns400BadRequest()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();
            var adminId = Guid.NewGuid();

            var currentOwner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"Owner_{uniqueSuffix}",
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = $"owner_{uniqueSuffix}@test.pl"
            };

            var adminUser = new ApplicationUser
            {
                Id = adminId,
                UserName = $"Admin_{uniqueSuffix}",
                FirstName = "Admin",
                LastName = "Systemowy",
                Email = $"admin_{uniqueSuffix}@test.pl"
            };

            var adminRole = new IdentityRole<Guid>
            {
                Id = Guid.NewGuid(),
                Name = "Admin",
                NormalizedName = "ADMIN"
            };

            var userRole = new IdentityUserRole<Guid>
            {
                UserId = adminId,
                RoleId = adminRole.Id
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Company_{uniqueSuffix}",
                NIP = "1112223334",
                OwnerId = ownerId,
                Owner = currentOwner
            };

            _contextMock.Users.AddRange(currentOwner, adminUser);
            _contextMock.Roles.Add(adminRole);
            _contextMock.UserRoles.Add(userRole);
            _contextMock.Companies.Add(company);
            await _contextMock.SaveChangesAsync();

            var command = new ChangeCompanyOwnerCommand
            {
                CompanyId = company.Id,
                UserId = adminId
            };

            // Act
            var result = await _companyServicesMock.ChangeCompanyOwnerAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.Message).IsEqualTo("Cannot assign company ownership to an admin user.");

            var unmodifiedCompany = await _contextMock.Companies.FindAsync(company.Id);
            await Assert.That(unmodifiedCompany).IsNotNull();
            await Assert.That(unmodifiedCompany!.OwnerId).IsEqualTo(ownerId);
        }

        [Test]
        public async Task ChangeCompanyOwnerAsync_WhenTargetUserIsValid_ChangesOwnerSuccessfully()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var oldOwnerId = Guid.NewGuid();
            var newOwnerId = Guid.NewGuid();

            var oldOwner = new ApplicationUser
            {
                Id = oldOwnerId,
                UserName = $"OldOwner_{uniqueSuffix}",
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = $"old_owner_{uniqueSuffix}@test.pl"
            };

            var newOwner = new ApplicationUser
            {
                Id = newOwnerId,
                UserName = $"NewOwner_{uniqueSuffix}",
                FirstName = "Marek",
                LastName = "Nowak",
                Email = $"new_owner_{uniqueSuffix}@test.pl"
            };

            var userRoleDefinition = new IdentityRole<Guid>
            {
                Id = Guid.NewGuid(),
                Name = "User",
                NormalizedName = "USER"
            };

            var assignedRole = new IdentityUserRole<Guid>
            {
                UserId = newOwnerId,
                RoleId = userRoleDefinition.Id
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"TransferCompany_{uniqueSuffix}",
                NIP = "1112223334",
                OwnerId = oldOwnerId,
                Owner = oldOwner
            };

            _contextMock.Users.AddRange(oldOwner, newOwner);
            _contextMock.Roles.Add(userRoleDefinition);
            _contextMock.UserRoles.Add(assignedRole);
            _contextMock.Companies.Add(company);
            await _contextMock.SaveChangesAsync();

            _contextMock.ChangeTracker.Clear();

            var command = new ChangeCompanyOwnerCommand
            {
                CompanyId = company.Id,
                UserId = newOwnerId
            };

            // Act
            var result = await _companyServicesMock.ChangeCompanyOwnerAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("Company ownership changed successfully.");

            var updatedCompany = await _contextMock.Companies.FindAsync(company.Id);
            await Assert.That(updatedCompany).IsNotNull();
            await Assert.That(updatedCompany!.OwnerId).IsEqualTo(newOwnerId);
        }
    }
}
