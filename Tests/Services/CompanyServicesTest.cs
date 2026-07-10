using Domain.Enum;
using Domain.Models;
using DTO.Request;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using Npgsql;
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

            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(_connectionString, options =>
                {
                    options.UseNetTopologySuite();
                })
                .Options;
            using var context = new AppDbContext(dbOptions);
            await context.Database.EnsureCreatedAsync();

            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
        }

        [After(Class)]
        public static async Task CleanupClassAsync()
            => await _dbContainer.DisposeAsync();

        [Before(Test)]
        public async Task SetupAsync()
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(_connectionString, options =>
                {
                    options.UseNetTopologySuite();
                })
                .Options;

            _contextMock = new AppDbContext(dbOptions);
            await _contextMock.Database.EnsureCreatedAsync();

            _loggerMock = new LoggerFactory().CreateLogger<CompanyServices>();

            _companyServicesMock = new CompanyServices(_contextMock, _loggerMock);
        }

        [After(Test)]
        public async Task CleanupAsync()
        {
            if (_contextMock != null)
            {
                await _contextMock.DisposeAsync();
            }
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
                Location = new Point(21.0122, 52.2297) { SRID = 4326 },
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
                Location = new Point(19.9449, 50.0646) { SRID = 4326 }
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
                Location = new Point(18.6466, 54.3520) { SRID = 4326 }
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
                Location = new Point(19.9383, 50.0614) { SRID = 4326 },
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

            var paggedRequest = new PaggedRequest { PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _companyServicesMock.GetCompanyAddresses(targetCompany.Id, paggedRequest);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var items = result.Data!.Items;
            await Assert.That(items).HasCount().EqualTo(1);

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

            var paggedRequest = new PaggedRequest { PageNumber = 1, PageSize = 2 };

            // Act
            var result = await _companyServicesMock.GetCompanyAddresses(company.Id, paggedRequest);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            await Assert.That(result.Data!.Items).HasCount().EqualTo(2);

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

            var pagged = new PaggedRequest
            {
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _companyServicesMock.GetCompanyListAsync(
                myUserId,
                pagged,
                new CompanyFilterRequest(),
                new SortingRequest(),
                new SearchRequest()
                );

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();

            var returnedCompanies = result.Data!.Items
                .Where(c => c.Name.Contains(uniqueSuffix))
                .ToList();

            await Assert.That(returnedCompanies).HasCount().EqualTo(2);

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

            var pagged = new PaggedRequest
            {
                PageNumber = 1,
                PageSize = 10
            };
            var filter = new CompanyFilterRequest { IsYour = true };

            // Act
            var result = await _companyServicesMock.GetCompanyListAsync(
                userId,
                pagged,
                filter,
                new SortingRequest(),
                new SearchRequest()
                );

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

            var pagged = new PaggedRequest { PageNumber = 1, PageSize = 10 };
            var search = new SearchRequest { SearchTerm = "apple" };
            var sorting = new SortingRequest { SortBy = "name", SortDescending = true };

            // Act
            var result = await _companyServicesMock.GetCompanyListAsync(userId, pagged, new CompanyFilterRequest(), sorting, search);

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

            var deletedCompany = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Deleted_{uniqueSuffix}",
                NIP = "111",
                OwnerId = userId,
                Owner = user,
                IsDeleted = true,
                CreatedAt = now
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
                CreatedAt = now.AddDays(-10)
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
                CreatedAt = now.AddDays(-2)
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

            var pagged = new PaggedRequest { PageNumber = 1, PageSize = 10 };

            var filter = new CompanyFilterRequest
            {
                CreatedAtFrom = now.AddDays(-5),
                CreatedAtTo = now
            };

            var sorting = new SortingRequest();
            var search = new SearchRequest();

            // Act
            var result = await _companyServicesMock.GetCompanyListAsync(userId, pagged, filter, sorting, search);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var items = result.Data!.Items;

            await Assert.That(items.Any(c => c.Id == deletedCompany.Id)).IsFalse();
            await Assert.That(items.Any(c => c.Id == oldCompany.Id)).IsFalse();
            await Assert.That(items.Any(c => c.Id == validCompany.Id)).IsTrue();
        }
    }
}
