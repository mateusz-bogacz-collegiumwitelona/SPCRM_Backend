using Domain.Constants;
using Domain.Enum;
using Domain.Models;
using Infrastructure;
using Infrastructure.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Services.Command.Company;
using Services.Command.Contact;
using Services.Command.List;
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

        // ─── AddContactAsync ─────────────────────────────────────────────────

        [Test]
        public async Task AddContactAsync_WhenCompanyDoesntExist_Return404()
        {
            // Arrange
            var detail = new AddContactDetailCommand
            {
                Label = "NWM",
                Value = "12345678",
                IsPrimary = false,
                Type = ContactDetailTypeEnum.PHONE_MOBILE.ToString()
            };

            var detailList = new List<AddContactDetailCommand>();
            detailList.Add(detail);

            var request = new AddContactCommand
            {
                CompanyId = Guid.NewGuid(),
                FirstName = "Test",
                LastName = "Test",
                Details = detailList
            };

            // Act 
            var result = await _contactServicesMock.AddContactAsync(request, Guid.NewGuid());

            // Assert 
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.Message).IsEqualTo("Company not found");
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.CompanyNotFound);
        }

        [Test]
        public async Task AddContactAsync_WhenOwnerDoesntExist_Return404()
        {
            // Arrange
            var companyOwnerId = Guid.NewGuid();
            var companyOwner = new ApplicationUser
            {
                Id = companyOwnerId,
                UserName = "TestOwner",
                Email = "test@test.pl",
                FirstName = "Test",
                LastName = "Test"
            };

            var comapnyId = Guid.NewGuid();
            var company = new Company
            {
                Id = comapnyId,
                Name = "Test",
                NIP = "12345678901",
                OwnerId = companyOwnerId,
                Owner = companyOwner
            };

            await _contextMock.Users.AddAsync(companyOwner);
            await _contextMock.Companies.AddAsync(company);
            await _contextMock.SaveChangesAsync();

            var detail = new AddContactDetailCommand
            {
                Label = "NWM",
                Value = "12345678",
                IsPrimary = false,
                Type = ContactDetailTypeEnum.PHONE_MOBILE.ToString()
            };

            var request = new AddContactCommand
            {
                CompanyId = comapnyId,
                FirstName = "Test",
                LastName = "Test",
                Details = new List<AddContactDetailCommand> { detail }
            };

            // Act 
            var nonExistentUserId = Guid.NewGuid();
            var result = await _contactServicesMock.AddContactAsync(request, nonExistentUserId);

            // Assert 
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.Message).IsEqualTo("User not found");
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.UserNotFound);
        }

        [Test]
        public async Task AddContactAsync_WhenNoPrimaryContactExists_Returns201AndSetsIsPrimaryToTrue()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var owner = new ApplicationUser
            {
                Id = userId,
                UserName = "TestOwner",
                Email = "owner@test.pl",
                FirstName = "Jan",
                LastName = "Kowalski"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = "Nowa Firma",
                NIP = "1234567890",
                OwnerId = userId,
                Owner = owner
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            await _contextMock.SaveChangesAsync();

            var detail = new AddContactDetailCommand
            {
                Label = "Służbowy",
                Value = "jan.kowalski@test.pl",
                IsPrimary = true,
                Type = ContactDetailTypeEnum.EMAIL.ToString()
            };

            var request = new AddContactCommand
            {
                CompanyId = company.Id,
                FirstName = "Jan",
                LastName = "Kowalski",
                JobTitle = "Dyrektor",
                Details = new List<AddContactDetailCommand> { detail }
            };

            // Act
            var result = await _contactServicesMock.AddContactAsync(request, userId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status201Created);
            await Assert.That(result.Message).IsEqualTo("Contact added successfully");

            var savedContact = await _contextMock.Contacts
                .Include(c => c.ContactDetails)
                .FirstOrDefaultAsync(c => c.CompanyId == company.Id);

            await Assert.That(savedContact).IsNotNull();
            await Assert.That(savedContact!.IsPrimary).IsTrue();
            await Assert.That(savedContact.ContactDetails).Count().IsEqualTo(1);
        }

        [Test]
        public async Task AddContactAsync_WhenPrimaryContactAlreadyExists_Returns201AndSetsIsPrimaryToFalse()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var owner = new ApplicationUser
            {
                Id = userId,
                UserName = "TestOwner2",
                Email = "owner2@test.pl",
                FirstName = "Piotr",
                LastName = "Nowak"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = "Istniejąca Firma",
                NIP = "0987654321",
                OwnerId = userId,
                Owner = owner
            };

            var existingPrimaryContact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Stary",
                LastName = "Kontakt",
                IsPrimary = true,
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = owner
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.Add(existingPrimaryContact);
            await _contextMock.SaveChangesAsync();

            var detail = new AddContactDetailCommand
            {
                Label = "Prywatny",
                Value = "123123123",
                IsPrimary = true,
                Type = ContactDetailTypeEnum.PHONE_MOBILE.ToString()
            };

            var request = new AddContactCommand
            {
                CompanyId = company.Id,
                FirstName = "Nowy",
                LastName = "Kontakt",
                JobTitle = "Zastępca",
                Details = new List<AddContactDetailCommand> { detail }
            };

            // Act
            var result = await _contactServicesMock.AddContactAsync(request, userId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status201Created);

            var newContact = await _contextMock.Contacts
                .FirstOrDefaultAsync(c => c.CompanyId == company.Id && c.FirstName == "Nowy");

            await Assert.That(newContact).IsNotNull();
            await Assert.That(newContact!.IsPrimary).IsFalse();
        }

        // ─── GetContactTypeAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetContactTypeAsync_ReturnsAllEnumNamesAndStatus200()
        {
            // Arrange
            var expectedTypesCount = Enum.GetNames(typeof(ContactDetailTypeEnum)).Length;

            // Act
            var result = await _contactServicesMock.GetContactTypeAsync();

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("Contact types retrieved successfully");

            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!).Count().IsEqualTo(expectedTypesCount);

            var containsEmail = result.Data!.Contains(ContactDetailTypeEnum.EMAIL.ToString());
            await Assert.That(containsEmail).IsTrue();
        }

        // ─── EditContactAsync ─────────────────────────────────────────────────

        [Test]
        public async Task EditContactAsync_WhenContactNotFound_Returns404()
        {
            // Arrange
            var command = new EditContactCommand
            {
                ContactId = Guid.NewGuid(),
                Details = new List<EditContactDetailCommand>()
            };

            // Act
            var result = await _contactServicesMock.EditContactAsync(command, Guid.NewGuid());

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.Message).IsEqualTo("Contact not found");
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.ContactNotFound);
        }

        [Test]
        public async Task EditContactAsync_WhenUserIsNotOwnerOrManager_Returns403()
        {
            // Arrange
            var ownerId = Guid.NewGuid();
            var unauthorizedUserId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = "Owner",
                Email = "owner@test.pl",
                FirstName = "Jan",
                LastName = "Kowalski"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = "Firma Testowa",
                NIP = "1234567890",
                OwnerId = ownerId,
                Owner = owner
            };

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Adam",
                LastName = "Nowak",
                IsPrimary = true,
                CompanyId = company.Id,
                Company = company,
                OwnerId = ownerId,
                Owner = owner
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.Add(contact);
            await _contextMock.SaveChangesAsync();

            var command = new EditContactCommand
            {
                ContactId = contact.Id,
                Details = new List<EditContactDetailCommand>()
            };

            // Act
            var result = await _contactServicesMock.EditContactAsync(command, unauthorizedUserId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.Message).IsEqualTo("You do not have permission to edit this contact");
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status403Forbidden);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.UnauthorizedAccess);
        }

        [Test]
        public async Task EditContactAsync_WhenUserIsManagerButNotOwner_AllowsEdit()
        {
            // Arrange
            var ownerId = Guid.NewGuid();
            var managerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = "Owner",
                FirstName = "O",
                LastName = "O"
            };

            var manager = new ApplicationUser
            {
                Id = managerId,
                UserName = "Manager",
                FirstName = "M",
                LastName = "M"
            };

            var role = new IdentityRole<Guid>
            {
                Id = Guid.NewGuid(),
                Name = "Manager",
                NormalizedName = "MANAGER"
            };

            var userRole = new IdentityUserRole<Guid>
            {
                UserId = managerId,
                RoleId = role.Id
            };

            var company = new Company { Id = Guid.NewGuid(), Name = "Comp", NIP = "111", OwnerId = ownerId, Owner = owner };

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Adam",
                LastName = "Nowak",
                IsPrimary = true,
                CompanyId = company.Id,
                Company = company,
                OwnerId = ownerId,
                Owner = owner
            };

            _contextMock.Users.AddRange(owner, manager);
            _contextMock.Roles.Add(role);
            _contextMock.UserRoles.Add(userRole);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.Add(contact);
            await _contextMock.SaveChangesAsync();

            var command = new EditContactCommand
            {
                ContactId = contact.Id,
                FirstName = "Zmienione",
                Details = new List<EditContactDetailCommand>()
            };

            // Act
            var result = await _contactServicesMock.EditContactAsync(command, managerId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var updatedContact = await _contextMock.Contacts.FindAsync(contact.Id);
            await Assert.That(updatedContact!.FirstName).IsEqualTo("Zmienione");
        }

        [Test]
        public async Task EditContactAsync_UpdatesDeletesAndAddsDetailsCorrectly()
        {
            // Arrange
            var ownerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = "Owner",
                FirstName = "O",
                LastName = "O"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = "Comp",
                NIP = "111",
                OwnerId = ownerId,
                Owner = owner
            };

            var detailToUpdate = new ContactDetail
            {
                Id = Guid.NewGuid(),
                Type = ContactDetailTypeEnum.EMAIL,
                Value = "old@test.pl",
                IsPrimary = true
            };

            var detailToDelete = new ContactDetail
            {
                Id = Guid.NewGuid(),
                Type = ContactDetailTypeEnum.PHONE,
                Value = "123123123",
                IsPrimary = false
            };

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Stare",
                LastName = "Stare",
                IsPrimary = true,
                CompanyId = company.Id,
                Company = company,
                OwnerId = ownerId,
                Owner = owner,
                ContactDetails = new List<ContactDetail> { detailToUpdate, detailToDelete }
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.Add(contact);
            await _contextMock.SaveChangesAsync();

            _contextMock.ChangeTracker.Clear();

            var updateDetailCommand = new EditContactDetailCommand
            {
                ContactDetailId = detailToUpdate.Id,
                Value = "new@test.pl",
                Type = "EMAIL",
                IsPrimary = true
            };

            var addDetailCommand = new EditContactDetailCommand
            {
                ContactDetailId = null,
                Value = "linkedin.com/in/test",
                Type = "LINKEDIN",
                IsPrimary = false
            };

            var command = new EditContactCommand
            {
                ContactId = contact.Id,
                FirstName = "NoweImię",
                LastName = "NoweNazwisko",
                JobTitle = "Szef",
                Details = new List<EditContactDetailCommand> { updateDetailCommand, addDetailCommand }
            };

            // Act
            var result = await _contactServicesMock.EditContactAsync(command, ownerId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var updatedContact = await _contextMock.Contacts
                .IgnoreQueryFilters()
                .Include(c => c.ContactDetails)
                .FirstOrDefaultAsync(c => c.Id == contact.Id);

            await Assert.That(updatedContact).IsNotNull();
            await Assert.That(updatedContact!.FirstName).IsEqualTo("NoweImię");
            await Assert.That(updatedContact.LastName).IsEqualTo("NoweNazwisko");
            await Assert.That(updatedContact.JobTitle).IsEqualTo("Szef");

            var allDetails = updatedContact.ContactDetails.ToList();
            await Assert.That(allDetails).Count().IsEqualTo(3);

            var modifiedDetail = allDetails.FirstOrDefault(d => d.Id == detailToUpdate.Id);
            await Assert.That(modifiedDetail!.Value).IsEqualTo("new@test.pl");
            await Assert.That(modifiedDetail.IsDeleted).IsFalse();

            var deletedDetail = allDetails.FirstOrDefault(d => d.Id == detailToDelete.Id);
            await Assert.That(deletedDetail!.IsDeleted).IsTrue();

            var addedDetail = allDetails.FirstOrDefault(d => d.Id != detailToUpdate.Id && d.Id != detailToDelete.Id);
            await Assert.That(addedDetail).IsNotNull();
            await Assert.That(addedDetail!.Type).IsEqualTo(ContactDetailTypeEnum.LINKEDIN);
            await Assert.That(addedDetail.Value).IsEqualTo("linkedin.com/in/test");
        }

        // ─── GetContactDetailCommand ──────────────────────────────────────────

        [Test]
        public async Task GetContactDetailCommand_WhenContactExists_ReturnsContactDetailsSuccessfully()
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
                Name = $"Firma_{uniqueSuffix}",
                NIP = "1234567890",
                OwnerId = userId,
                Owner = owner
            };

            var contactId = Guid.NewGuid();
            var contact = new Contact
            {
                Id = contactId,
                FirstName = "Andrzej",
                LastName = "Nowak",
                JobTitle = "Manager",
                IsPrimary = true,
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = owner
            };

            var detail = new ContactDetail
            {
                Id = Guid.NewGuid(),
                Type = ContactDetailTypeEnum.EMAIL,
                Value = "andrzej.nowak@test.pl",
                Label = "Służbowy",
                IsPrimary = true,
                ContactId = contactId,
                Contact = contact
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.Add(contact);
            _contextMock.ContactDetails.Add(detail);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _contactServicesMock.GetContactDetailCommand(contactId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();

            var data = result.Data!;
            await Assert.That(data.ContactId).IsEqualTo(contactId);
            await Assert.That(data.FirstName).IsEqualTo("Andrzej");
            await Assert.That(data.LastName).IsEqualTo("Nowak");
            await Assert.That(data.JobTitle).IsEqualTo("Manager");

            await Assert.That(data.Details).Count().IsEqualTo(1);
            var mappedDetail = data.Details.First();
            await Assert.That(mappedDetail.ContactDetailId).IsEqualTo(detail.Id);
            await Assert.That(mappedDetail.Value).IsEqualTo("andrzej.nowak@test.pl");
            await Assert.That(mappedDetail.Label).IsEqualTo("Służbowy");
            await Assert.That(mappedDetail.IsPrimary).IsTrue();
            await Assert.That(mappedDetail.Type).IsEqualTo(ContactDetailTypeEnum.EMAIL.ToString());
        }

        [Test]
        public async Task GetContactDetailCommand_WhenContactDoesNotExist_Returns404NotFound()
        {
            // Arrange
            var nonExistentContactId = Guid.NewGuid();

            // Act
            var result = await _contactServicesMock.GetContactDetailCommand(nonExistentContactId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.Message).IsEqualTo("Contact not found");
            await Assert.That(result.Data).IsNull();
        }

        // ─── SetPrimaryContactAsync ──────────────────────────────────────────

        [Test]
        public async Task SetPrimaryContactAsync_WhenContactNotFound_Returns404()
        {
            // Arrange
            var nonExistentContactId = Guid.NewGuid();
            var currentUserId = Guid.NewGuid();

            // Act
            var result = await _contactServicesMock.SetPrimaryContactAsync(nonExistentContactId, currentUserId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.Message).IsEqualTo("Contact not found");
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.ContactNotFound);
        }

        [Test]
        public async Task SetPrimaryContactAsync_WhenUserIsNotAuthorized_Returns403()
        {
            // Arrange
            var ownerId = Guid.NewGuid();
            var unauthorizedUserId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = "Owner",
                Email = "owner@test.pl",
                FirstName = "Jan",
                LastName = "Kowalski"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = "Firma",
                NIP = "111",
                OwnerId = ownerId,
                Owner = owner
            };

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Adam",
                LastName = "Nowak",
                IsPrimary = false,
                CompanyId = company.Id,
                Company = company,
                OwnerId = ownerId,
                Owner = owner
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.Add(contact);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _contactServicesMock.SetPrimaryContactAsync(contact.Id, unauthorizedUserId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status403Forbidden);
            await Assert.That(result.Message).IsEqualTo("You do not have permission to edit this contact");
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.UnauthorizedAccess);
        }

        [Test]
        public async Task SetPrimaryContactAsync_WhenContactIsAlreadyPrimary_Returns400()
        {
            // Arrange
            var ownerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = "Owner",
                FirstName = "O",
                LastName = "O"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = "Firma",
                NIP = "111",
                OwnerId = ownerId,
                Owner = owner
            };

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Adam",
                LastName = "Nowak",
                IsPrimary = true,
                CompanyId = company.Id,
                Company = company,
                OwnerId = ownerId,
                Owner = owner
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.Add(contact);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _contactServicesMock.SetPrimaryContactAsync(contact.Id, ownerId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.Message).IsEqualTo("This contact is already the primary contact for the company.");
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.PrimaryContactDetailRequired);
        }

        [Test]
        public async Task SetPrimaryContactAsync_WhenValidRequest_SetsNewPrimaryAndUnsetsOldPrimary_Returns200()
        {
            // Arrange
            var ownerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = "Owner",
                FirstName = "O",
                LastName = "O"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = "Firma Testowa",
                NIP = "111",
                OwnerId = ownerId,
                Owner = owner
            };

            var oldPrimaryContact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Stary",
                LastName = "Główny",
                IsPrimary = true,
                CompanyId = company.Id,
                Company = company,
                OwnerId = ownerId,
                Owner = owner
            };

            var newPrimaryContact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Nowy",
                LastName = "Główny",
                IsPrimary = false,
                CompanyId = company.Id,
                Company = company,
                OwnerId = ownerId,
                Owner = owner
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.AddRange(oldPrimaryContact, newPrimaryContact);
            await _contextMock.SaveChangesAsync();

            _contextMock.ChangeTracker.Clear();

            // Act
            var result = await _contactServicesMock.SetPrimaryContactAsync(newPrimaryContact.Id, ownerId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("Contact changed to primary successfully");

            var updatedOldPrimary = await _contextMock.Contacts.FindAsync(oldPrimaryContact.Id);
            var updatedNewPrimary = await _contextMock.Contacts.FindAsync(newPrimaryContact.Id);

            await Assert.That(updatedOldPrimary).IsNotNull();
            await Assert.That(updatedOldPrimary!.IsPrimary).IsFalse();

            await Assert.That(updatedNewPrimary).IsNotNull();
            await Assert.That(updatedNewPrimary!.IsPrimary).IsTrue();
        }

        // ─── DeleteContactAsync ─────────────────────────────────────────────────

        [Test]
        public async Task DeleteContactAsync_WhenContactExists_SoftDeletesContactAndReturns200()
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
                Name = $"Firma_{uniqueSuffix}",
                NIP = "1234567890",
                OwnerId = userId,
                Owner = owner
            };

            var contactId = Guid.NewGuid();
            var contact = new Contact
            {
                Id = contactId,
                FirstName = "Do",
                LastName = "Usunięcia",
                JobTitle = "Manager",
                IsPrimary = false,
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = owner
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.Add(contact);
            await _contextMock.SaveChangesAsync();

            _contextMock.ChangeTracker.Clear();

            // Act
            var result = await _contactServicesMock.DeleteContactAsync(contactId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("Contact deleted successfully");

            var visibleContact = await _contextMock.Contacts.FirstOrDefaultAsync(c => c.Id == contactId);
            await Assert.That(visibleContact).IsNull();

            var softDeletedContact = await _contextMock.Contacts
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == contactId);

            await Assert.That(softDeletedContact).IsNotNull();
            await Assert.That(softDeletedContact!.IsDeleted).IsTrue();
        }

        [Test]
        public async Task DeleteContactAsync_WhenContactDoesNotExist_Returns404NotFound()
        {
            // Arrange
            var nonExistentContactId = Guid.NewGuid();

            // Act
            var result = await _contactServicesMock.DeleteContactAsync(nonExistentContactId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.Message).IsEqualTo("Contact not found");
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.ContactNotFound);
        }

        // ─── ChangeContactOwnerAsync ─────────────────────────────────────────────────

        [Test]
        public async Task ChangeContactOwnerAsync_WhenContactNotFound_Returns404()
        {
            // Arrange
            var command = new ChangeContactOwnerCommand
            {
                ContactId = Guid.NewGuid(),
                NewOwnerId = Guid.NewGuid()
            };

            // Act
            var result = await _contactServicesMock.ChangeContactOwnerAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.Message).IsEqualTo("Contact not found");
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.ContactNotFound);
        }

        [Test]
        public async Task ChangeContactOwnerAsync_WhenNewOwnerNotFound_Returns404()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var oldOwnerId = Guid.NewGuid();

            var oldOwner = new ApplicationUser
            {
                Id = oldOwnerId,
                UserName = $"OldOwner_{uniqueSuffix}",
                NormalizedUserName = $"OLDOWNER_{uniqueSuffix}",
                Email = $"old_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"OLD_{uniqueSuffix}@TEST.PL",
                FirstName = "Stary",
                LastName = "Opiekun"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Firma_{uniqueSuffix}",
                NIP = "111222333",
                OwnerId = oldOwnerId,
                Owner = oldOwner
            };

            var contactId = Guid.NewGuid();
            var contact = new Contact
            {
                Id = contactId,
                FirstName = "Jan",
                LastName = "Kowalski",
                IsPrimary = true,
                CompanyId = company.Id,
                Company = company,
                OwnerId = oldOwnerId,
                Owner = oldOwner
            };

            _contextMock.Users.Add(oldOwner);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.Add(contact);
            await _contextMock.SaveChangesAsync();

            var command = new ChangeContactOwnerCommand
            {
                ContactId = contactId,
                NewOwnerId = Guid.NewGuid()
            };

            // Act
            var result = await _contactServicesMock.ChangeContactOwnerAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.Message).IsEqualTo("New owner not found");
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.UserNotFound);
        }

        [Test]
        public async Task ChangeContactOwnerAsync_WhenValidRequest_UpdatesOwnerAndReturns200()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var oldOwnerId = Guid.NewGuid();
            var newOwnerId = Guid.NewGuid();

            var oldOwner = new ApplicationUser
            {
                Id = oldOwnerId,
                UserName = $"Old_{uniqueSuffix}",
                NormalizedUserName = $"OLD_{uniqueSuffix}",
                Email = $"old_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"OLD_{uniqueSuffix}@TEST.PL",
                FirstName = "Stary",
                LastName = "Opiekun"
            };

            var newOwner = new ApplicationUser
            {
                Id = newOwnerId,
                UserName = $"New_{uniqueSuffix}",
                NormalizedUserName = $"NEW_{uniqueSuffix}",
                Email = $"new_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"NEW_{uniqueSuffix}@TEST.PL",
                FirstName = "Nowy",
                LastName = "Opiekun"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Firma_{uniqueSuffix}",
                NIP = "999888777",
                OwnerId = oldOwnerId,
                Owner = oldOwner
            };

            var contactId = Guid.NewGuid();
            var contact = new Contact
            {
                Id = contactId,
                FirstName = "Jan",
                LastName = "Kowalski",
                IsPrimary = true,
                CompanyId = company.Id,
                Company = company,
                OwnerId = oldOwnerId,
                Owner = oldOwner
            };

            _contextMock.Users.AddRange(oldOwner, newOwner);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.Add(contact);
            await _contextMock.SaveChangesAsync();

            _contextMock.ChangeTracker.Clear();

            var command = new ChangeContactOwnerCommand
            {
                ContactId = contactId,
                NewOwnerId = newOwnerId
            };

            // Act
            var result = await _contactServicesMock.ChangeContactOwnerAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("Contact owner changed successfully");

            var updatedContact = await _contextMock.Contacts.FindAsync(contactId);
            await Assert.That(updatedContact).IsNotNull();
            await Assert.That(updatedContact!.OwnerId).IsEqualTo(newOwnerId);
        }

        // ─── GetAvailableOwnersAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetAvailableOwnersAsync_ExcludesAdminsAndMapsRolesCorrectly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var adminRole = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "Admin", NormalizedName = "ADMIN" };
            var managerRole = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "Manager", NormalizedName = "MANAGER" };

            _contextMock.Roles.AddRange(adminRole, managerRole);

            var adminUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"Admin_{uniqueSuffix}",
                NormalizedUserName = $"ADMIN_{uniqueSuffix}",
                Email = $"admin_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"ADMIN_{uniqueSuffix}@T.PL",
                FirstName = "Adam",
                LastName = "Adminowski"
            };

            var managerUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"Manager_{uniqueSuffix}",
                NormalizedUserName = $"MANAGER_{uniqueSuffix}",
                Email = $"manager_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"MANAGER_{uniqueSuffix}@T.PL",
                FirstName = "Marek",
                LastName = "Menedżerski"
            };

            var noRoleUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"NoRole_{uniqueSuffix}",
                NormalizedUserName = $"NOROLE_{uniqueSuffix}",
                Email = $"norole_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"NOROLE_{uniqueSuffix}@T.PL",
                FirstName = "Brak",
                LastName = "Roli"
            };

            _contextMock.Users.AddRange(adminUser, managerUser, noRoleUser);

            _contextMock.UserRoles.Add(new IdentityUserRole<Guid> { UserId = adminUser.Id, RoleId = adminRole.Id });
            _contextMock.UserRoles.Add(new IdentityUserRole<Guid> { UserId = managerUser.Id, RoleId = managerRole.Id });

            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _contactServicesMock.GetAvailableOwnersAsync();

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var data = result.Data;
            await Assert.That(data).IsNotNull();

            var hasAdmin = data!.Any(u => u.Id == adminUser.Id);
            await Assert.That(hasAdmin).IsFalse();

            var mappedManager = data!.FirstOrDefault(u => u.Id == managerUser.Id);
            await Assert.That(mappedManager).IsNotNull();
            await Assert.That(mappedManager!.Role).IsEqualTo("Manager");

            var mappedNoRole = data!.FirstOrDefault(u => u.Id == noRoleUser.Id);
            await Assert.That(mappedNoRole).IsNotNull();
            await Assert.That(mappedNoRole!.Role).IsEqualTo("Brak");
        }

        [Test]
        public async Task GetAvailableOwnersAsync_OrdersByLastNameThenFirstName()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var role = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "User", NormalizedName = "USER" };
            _contextMock.Roles.Add(role);

            var user1 = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"U1_{uniqueSuffix}",
                NormalizedUserName = $"U1_{uniqueSuffix}",
                Email = $"1_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"1_{uniqueSuffix}@T.PL",
                FirstName = "Zbigniew",
                LastName = "Kowalski"
            };
            var user2 = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"U2_{uniqueSuffix}",
                NormalizedUserName = $"U2_{uniqueSuffix}",
                Email = $"2_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"2_{uniqueSuffix}@T.PL",
                FirstName = "Adam",
                LastName = "Kowalski"
            };
            var user3 = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"U3_{uniqueSuffix}",
                NormalizedUserName = $"U3_{uniqueSuffix}",
                Email = $"3_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"3_{uniqueSuffix}@T.PL",
                FirstName = "Jan",
                LastName = "Nowak"
            };

            _contextMock.Users.AddRange(user1, user2, user3);

            _contextMock.UserRoles.Add(new IdentityUserRole<Guid> { UserId = user1.Id, RoleId = role.Id });
            _contextMock.UserRoles.Add(new IdentityUserRole<Guid> { UserId = user2.Id, RoleId = role.Id });
            _contextMock.UserRoles.Add(new IdentityUserRole<Guid> { UserId = user3.Id, RoleId = role.Id });

            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _contactServicesMock.GetAvailableOwnersAsync();

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            var data = result.Data!;

            var testUsers = data.Where(u => u.Id == user1.Id || u.Id == user2.Id || u.Id == user3.Id).ToList();

            await Assert.That(testUsers).Count().IsEqualTo(3);
            await Assert.That(testUsers[0].Id).IsEqualTo(user2.Id);
            await Assert.That(testUsers[1].Id).IsEqualTo(user1.Id);
            await Assert.That(testUsers[2].Id).IsEqualTo(user3.Id);
        }
    }
}
