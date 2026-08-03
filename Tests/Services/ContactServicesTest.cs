using Domain.Models;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Services.Command;
using Services.Services;
using Testcontainers.PostgreSql;

namespace Tests.Services
{
    public class ContactServicesTest
    {
        protected AppDbContext _contextMock = null!;

        private static PostgreSqlContainer _dbContainer = null!;
        private static string _connectionString = null!;

        protected ContactServices _contactServicesMock = null!;
        protected ILogger<ContactServices> _loggerMock = null!;

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
                 .WithExposedPort(1025)
                 .Build();

            await _dbContainer.StartAsync();

            _connectionString = _dbContainer.GetConnectionString();

            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
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
                .Options;

            _contextMock = new AppDbContext(dbOptions);

            var createScript = _contextMock.Database.GenerateCreateScript();
            await _contextMock.Database.ExecuteSqlRawAsync(createScript);

            _loggerMock = new LoggerFactory().CreateLogger<ContactServices>();

            _contactServicesMock = new ContactServices(_contextMock, _loggerMock);
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

        // ─── GetContactsAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetContactsAsync_MapsPropertiesAndNavigationsCorrectly()
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
                FirstName = "Piotr",
                LastName = "Kowal"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"TechCorp_{uniqueSuffix}",
                NIP = "1234567890",
                OwnerId = userId,
                Owner = owner
            };

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Jan",
                LastName = "Nowak",
                JobTitle = "Dyrektor",
                IsPrimary = true,
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = owner
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.Add(contact);
            await _contextMock.SaveChangesAsync();

            var command = new ContactListCommand { PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _contactServicesMock.GetContactsAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var mappedContact = result.Data!.Items.FirstOrDefault(c => c.Id == contact.Id);
            await Assert.That(mappedContact).IsNotNull();

            await Assert.That(mappedContact!.CompanyName).IsEqualTo(company.Name);
            await Assert.That(mappedContact.OwnerFirstName).IsEqualTo(owner.FirstName);
            await Assert.That(mappedContact.OwnerLastName).IsEqualTo(owner.LastName);
            await Assert.That(mappedContact.JobTitle).IsEqualTo("Dyrektor");
            await Assert.That(mappedContact.IsPrimary).IsTrue();
        }

        [Test]
        public async Task GetContactsAsync_WhenSearchTermProvided_FiltersResultsCorrectly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = userId,
                FirstName = $"F_{uniqueSuffix}",
                LastName = $"L_{uniqueSuffix}",
                UserName = $"U_{uniqueSuffix}",
                NormalizedUserName = $"U_{uniqueSuffix}",
                Email = $"e_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"E_{uniqueSuffix}@T.PL"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Comp_{uniqueSuffix}",
                NIP = "111",
                OwnerId = userId,
                Owner = owner
            };

            var targetContact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Anna",
                LastName = "Zielińska",
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = owner,
                IsPrimary = true
            };

            var otherContact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Tomasz",
                LastName = "Malinowski",
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = owner,
                IsPrimary = false
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.AddRange(targetContact, otherContact);
            await _contextMock.SaveChangesAsync();

            var command = new ContactListCommand
            {
                PageNumber = 1,
                PageSize = 10,
                SearchTerm = "zielińska"
            };

            // Act
            var result = await _contactServicesMock.GetContactsAsync(command);

            await Assert.That(result.IsSuccess).IsTrue();
            var items = result.Data!.Items;

            await Assert.That(items.Any(c => c.Id == targetContact.Id)).IsTrue();
            await Assert.That(items.Any(c => c.Id == otherContact.Id)).IsFalse();
        }

        [Test]
        public async Task GetContactsAsync_AppliesPaginationCorrectly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = userId,
                UserName = $"Usr_{uniqueSuffix}",
                NormalizedUserName = $"USR_{uniqueSuffix}",
                FirstName = $"F_{uniqueSuffix}",
                LastName = $"L_{uniqueSuffix}",
                Email = $"mail_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"MAIL_{uniqueSuffix}@T.PL"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Cmp_{uniqueSuffix}",
                NIP = "222",
                OwnerId = userId,
                Owner = owner
            };

            var contacts = new List<Contact>
            {
                new Contact {
                    Id = Guid.NewGuid(),
                    FirstName = "K1",
                    LastName = "L1",
                    CompanyId = company.Id,
                    Company = company,
                    OwnerId = userId,
                    Owner = owner,
                    IsPrimary = true
                },

                new Contact {
                    Id = Guid.NewGuid(),
                    FirstName = "K2",
                    LastName = "L2",
                    CompanyId = company.Id,
                    Company = company,
                    OwnerId = userId,
                    Owner = owner,
                    IsPrimary = false
                },

                new Contact {
                    Id = Guid.NewGuid(),
                    FirstName = "K3",
                    LastName = "L3",
                    CompanyId = company.Id,
                    Company = company,
                    OwnerId = userId,
                    Owner = owner,
                    IsPrimary = false
                }
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.AddRange(contacts);
            await _contextMock.SaveChangesAsync();

            var command = new ContactListCommand { PageNumber = 1, PageSize = 2 };

            // Act
            var result = await _contactServicesMock.GetContactsAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();
        }

        // ─── GetContactsAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetCompaniesAsync_ReturnsDistinctCompanyNamesFromContactsOnly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = userId,
                UserName = $"User_{uniqueSuffix}",
                NormalizedUserName = $"USER_{uniqueSuffix}",
                Email = $"u_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"U_{uniqueSuffix}@TEST.PL",
                FirstName = "Jan",
                LastName = "Kowalski"
            };

            var companyA = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"CompanyA_{uniqueSuffix}",
                NIP = "111",
                OwnerId = userId,
                Owner = owner
            };

            var companyB = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"CompanyB_{uniqueSuffix}",
                NIP = "222",
                OwnerId = userId,
                Owner = owner
            };

            var emptyCompany = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"EmptyCompany_{uniqueSuffix}",
                NIP = "333",
                OwnerId = userId,
                Owner = owner
            };

            var contact1 = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Jan",
                LastName = "Kowalski",
                CompanyId = companyA.Id,
                Company = companyA,
                OwnerId = userId,
                Owner = owner,
                IsPrimary = true
            };

            var contact2 = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Anna",
                LastName = "Nowak",
                CompanyId = companyA.Id,
                Company = companyA,
                OwnerId = userId,
                Owner = owner,
                IsPrimary = false
            };

            var contact3 = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Piotr",
                LastName = "Zieliński",
                CompanyId = companyB.Id,
                Company = companyB,
                OwnerId = userId,
                Owner = owner,
                IsPrimary = false
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.AddRange(companyA, companyB, emptyCompany);
            _contextMock.Contacts.AddRange(contact1, contact2, contact3);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _contactServicesMock.GetCompaniesAsync();

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var companyNames = result.Data!;

            var companyACount = companyNames.Count(name => name == companyA.Name);
            await Assert.That(companyACount).IsEqualTo(1);

            var companyBCount = companyNames.Count(name => name == companyB.Name);
            await Assert.That(companyBCount).IsEqualTo(1);

            var containsEmptyCompany = companyNames.Contains(emptyCompany.Name);
            await Assert.That(containsEmptyCompany).IsFalse();
        }

        // ─── GetCompanyContactsAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetCompanyContactsAsync_FiltersByCompanyAndMapsPropertiesCorrectly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = userId,
                UserName = $"Usr_{uniqueSuffix}",
                NormalizedUserName = $"USR_{uniqueSuffix}",
                Email = $"u_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"U_{uniqueSuffix}@T.PL",
                FirstName = "Adam",
                LastName = "Kowalski"
            };

            var targetCompany = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Target_{uniqueSuffix}",
                NIP = "111",
                OwnerId = userId,
                Owner = owner
            };

            var otherCompany = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Other_{uniqueSuffix}",
                NIP = "222",
                OwnerId = userId,
                Owner = owner
            };

            var targetContactFull = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Jan",
                LastName = "Nowak",
                JobTitle = "CEO",
                IsPrimary = true,
                CompanyId = targetCompany.Id,
                Company = targetCompany,
                OwnerId = userId,
                Owner = owner
            };

            var targetContactNullJob = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Anna",
                LastName = "Z",
                JobTitle = null,
                IsPrimary = false,
                CompanyId = targetCompany.Id,
                Company = targetCompany,
                OwnerId = userId,
                Owner = owner
            };

            var otherContact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Piotr",
                LastName = "X",
                JobTitle = "Dev",
                IsPrimary = true,
                CompanyId = otherCompany.Id,
                Company = otherCompany,
                OwnerId = userId,
                Owner = owner
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.AddRange(targetCompany, otherCompany);
            _contextMock.Contacts.AddRange(targetContactFull, targetContactNullJob, otherContact);
            await _contextMock.SaveChangesAsync();

            var command = new CompanyCommand
            {
                PageNumber = 1,
                PageSize = 10,
                CompanyId = targetCompany.Id
            };

            // Act
            var result = await _contactServicesMock.GetCompanyContactsAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var items = result.Data!.Items;
            await Assert.That(items).Count().IsEqualTo(2);

            var mappedFullContact = items.First(c => c.Id == targetContactFull.Id);
            await Assert.That(mappedFullContact.FirstName).IsEqualTo("Jan");
            await Assert.That(mappedFullContact.OwnerFirstName).IsEqualTo("Adam");
            await Assert.That(mappedFullContact.OwnerLastName).IsEqualTo("Kowalski");

            var mappedNullJobContact = items.First(c => c.Id == targetContactNullJob.Id);
            await Assert.That(mappedNullJobContact.JobTitle).IsEqualTo("");
        }

        [Test]
        public async Task GetCompanyContactsAsync_AppliesPaginationCorrectly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = userId,
                UserName = $"U_{uniqueSuffix}",
                NormalizedUserName = $"U_{uniqueSuffix}",
                Email = $"e_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"E_{uniqueSuffix}@T.PL",
                FirstName = "Jan",
                LastName = "Kowalski"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Comp_{uniqueSuffix}",
                NIP = "123",
                OwnerId = userId,
                Owner = owner
            };

            var contacts = new List<Contact>
            {
                new Contact {
                    Id = Guid.NewGuid(),
                    FirstName = "K1",
                    LastName = "L1",
                    CompanyId = company.Id,
                    Company = company,
                    OwnerId = userId,
                    Owner = owner,
                    IsPrimary = true
                },

                new Contact {
                    Id = Guid.NewGuid(),
                    FirstName = "K2",
                    LastName = "L2",
                    CompanyId = company.Id,
                    Company = company,
                    OwnerId = userId,
                    Owner = owner,
                    IsPrimary = false
                },

                new Contact {
                    Id = Guid.NewGuid(),
                    FirstName = "K3",
                    LastName = "L3",
                    CompanyId = company.Id,
                    Company = company,
                    OwnerId = userId,
                    Owner = owner,
                    IsPrimary = false
                }
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.AddRange(contacts);
            await _contextMock.SaveChangesAsync();

            var command = new CompanyCommand { PageNumber = 1, PageSize = 2, CompanyId = company.Id };

            var result = await _contactServicesMock.GetCompanyContactsAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();
        }

        [Test]
        public async Task GetCompanyContactsAsync_WhenCompanyHasNoContacts_ReturnsEmptyList()
        {
            // Arrange
            var randomCompanyId = Guid.NewGuid();
            var command = new CompanyCommand { PageNumber = 1, PageSize = 10, CompanyId = randomCompanyId };

            // Act
            var result = await _contactServicesMock.GetCompanyContactsAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.Items).IsEmpty();
        }

        // ─── GetContactDetailAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetContactDetailAsync_WhenContactExist_Return200()
        {

            Guid userId = Guid.NewGuid();
            string uniqueSuffix = Guid.NewGuid().ToString("N");

            var owner = new ApplicationUser
            {
                Id = userId,
                UserName = $"U_{uniqueSuffix}",
                NormalizedUserName = $"U_{uniqueSuffix}",
                Email = $"e_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"E_{uniqueSuffix}@T.PL",
                FirstName = "Jan",
                LastName = "Kowalski"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Comp_{uniqueSuffix}",
                NIP = "123",
                OwnerId = userId,
                Owner = owner
            };

            Guid contactId = Guid.NewGuid();

            var contact = new Contact
            {
                Id = contactId,
                FirstName = "K3",
                LastName = "L3",
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = owner,
                IsPrimary = false
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.Add(contact);

            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _contactServicesMock.GetContactDetailAsync(contactId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Message).IsEqualTo("Contact details retrieved successfully");
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var data = result.Data;
            await Assert.That(data).IsNotNull();
            await Assert.That(data!.Id).IsEqualTo(contactId);
            await Assert.That(data.FirstName).IsEqualTo("K3");
            await Assert.That(data.LastName).IsEqualTo("L3");
            await Assert.That(data.IsPrimary).IsFalse();
        }

        [Test]
        public async Task GetContactDetailAsync_WhenContactNotExist_ReturnsNullData()
        {
            // Arrange & Act
            var result = await _contactServicesMock.GetContactDetailAsync(Guid.NewGuid());

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Message).IsEqualTo("Contact details retrieved successfully");
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNull();
        }

        // ─── GetClientDataToMailingAsync ───────────────────────────────────────

        [Test]
        public async Task GetClientDataToMailingAsync_FiltersPrimaryContactsAndMapsPropertiesCorrectly()
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
                LastName = "Kowalski"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"SteelCorp_{uniqueSuffix}",
                NIP = "9876543210",
                OwnerId = userId,
                Owner = owner
            };

            var primaryContact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Adam",
                LastName = "Nowak",
                IsPrimary = true,
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = owner
            };

            var secondaryContact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Piotr",
                LastName = "Zieliński",
                IsPrimary = false,
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = owner
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.AddRange(primaryContact, secondaryContact);
            await _contextMock.SaveChangesAsync();

            var command = new SimpleListCommand
            {
                PageNumber = 1,
                PageSize = 10,
                SearchTerm = uniqueSuffix
            };

            // Act
            var result = await _contactServicesMock.GetClientDataToMailingAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var items = result.Data!.Items;

            await Assert.That(items).Count().IsEqualTo(1);

            var mappedClient = items.First();
            await Assert.That(mappedClient.ContactId).IsEqualTo(primaryContact.Id);
            await Assert.That(mappedClient.ContactFirstName).IsEqualTo("Adam");
            await Assert.That(mappedClient.ContactLastName).IsEqualTo("Nowak");
            await Assert.That(mappedClient.CompanyName).IsEqualTo(company.Name);
            await Assert.That(mappedClient.Nip).IsEqualTo(company.NIP);
        }

        [Test]
        public async Task GetClientDataToMailingAsync_WhenSearchTermProvided_FiltersCorrectly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = userId,
                UserName = $"Usr_{uniqueSuffix}",
                NormalizedUserName = $"USR_{uniqueSuffix}",
                Email = $"u_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"U_{uniqueSuffix}@T.PL",
                FirstName = "Marek",
                LastName = "Mostowiak"
            };

            var matchingCompany = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Huta Katowice {uniqueSuffix}",
                NIP = "111222333",
                OwnerId = userId,
                Owner = owner
            };

            var otherCompany = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Inna Firma {uniqueSuffix}",
                NIP = "999888777",
                OwnerId = userId,
                Owner = owner
            };

            var matchingContact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Krzysztof",
                LastName = "Stalowy",
                IsPrimary = true,
                CompanyId = matchingCompany.Id,
                Company = matchingCompany,
                OwnerId = userId,
                Owner = owner
            };

            var otherContact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Janusz",
                LastName = "Drewniany",
                IsPrimary = true,
                CompanyId = otherCompany.Id,
                Company = otherCompany,
                OwnerId = userId,
                Owner = owner
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.AddRange(matchingCompany, otherCompany);
            _contextMock.Contacts.AddRange(matchingContact, otherContact);
            await _contextMock.SaveChangesAsync();

            var command = new SimpleListCommand
            {
                PageNumber = 1,
                PageSize = 10,
                SearchTerm = "Katowice"
            };

            // Act
            var result = await _contactServicesMock.GetClientDataToMailingAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            var items = result.Data!.Items;

            await Assert.That(items).Count().IsEqualTo(1);
            await Assert.That(items.First().ContactId).IsEqualTo(matchingContact.Id);
            await Assert.That(items.First().CompanyName).Contains("Katowice");
        }
    }
}
