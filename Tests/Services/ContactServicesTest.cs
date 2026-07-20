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
            _currentSchema = "test_schema_" + Guid.NewGuid().ToString("N");
            using var conn = new NpgsqlConnection(_connectionString);

            await conn.OpenAsync();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"CREATE SCHEMA {_currentSchema};";
                await cmd.ExecuteNonQueryAsync();
            }

            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(_connectionString, options =>
                {
                    options.UseNetTopologySuite();
                    options.MigrationsHistoryTable("__EFMigrationsHistory", _currentSchema);
                })
                .Options;

            _contextMock = new AppDbContext(dbOptions);
            await _contextMock.Database.ExecuteSqlRawAsync($"SET search_path TO {_currentSchema}");
            await _contextMock.Database.EnsureCreatedAsync();

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
            await Assert.That(items).HasCount().EqualTo(2);

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

        // ─── GetContactNoteAsync ─────────────────────────────────────────────────
        [Test]
        public async Task GetContactNoteAsync_FiltersDeletedAndOtherTypes_AndMapsCorrectly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userId = Guid.NewGuid();

            var author = new ApplicationUser
            {
                Id = userId,
                UserName = $"User_{uniqueSuffix}",
                NormalizedUserName = $"USER_{uniqueSuffix}",
                Email = $"u_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"U_{uniqueSuffix}@T.PL",
                FirstName = "Michał",
                LastName = "Pisarz"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Comp_{uniqueSuffix}",
                NIP = "123",
                OwnerId = userId,
                Owner = author
            };

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Jan",
                LastName = "Kowalski",
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = author,
                IsPrimary = true
            };

            var validNote = new ContactNote
            {
                Id = Guid.NewGuid(),
                Title = "Ważna notatka",
                Content = "Treść",
                ContactId = contact.Id,
                AuthorId = userId,
                Author = author,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            };

            var deletedNote = new ContactNote
            {
                Id = Guid.NewGuid(),
                Title = "Usunięta",
                Content = "Treść",
                ContactId = contact.Id,
                AuthorId = userId,
                Author = author,
                IsDeleted = true
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
                Name = "Deal",
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = author,
                CurrencyId = currency.Id,
                Currency = currency
            };

            var dealNote = new DealNote
            {
                Id = Guid.NewGuid(),
                Title = "Notatka Deal",
                Content = "Treść",
                DealId = deal.Id,
                AuthorId = userId,
                Author = author,
                IsDeleted = false
            };

            _contextMock.Users.Add(author);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.Add(contact);
            _contextMock.Currencies.Add(currency);
            _contextMock.Deals.Add(deal);
            _contextMock.Notes.AddRange(validNote, deletedNote, dealNote);
            await _contextMock.SaveChangesAsync();

            var command = new NoteListCommand
            {
                PageNumber = 1,
                PageSize = 10,
                searchId = contact.Id
            };

            // Act
            var result = await _contactServicesMock.GetContactNoteAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var items = result.Data!.Items;

            await Assert.That(items).HasCount().EqualTo(1);

            var mappedNote = items.First();
            await Assert.That(mappedNote.Id).IsEqualTo(validNote.Id);
            await Assert.That(mappedNote.Title).IsEqualTo("Ważna notatka");
            await Assert.That(mappedNote.AuthorFirstName).IsEqualTo("Michał");
            await Assert.That(mappedNote.AuthorLastName).IsEqualTo("Pisarz");
        }

        [Test]
        public async Task GetContactNoteAsync_SortsNotesByCreatedAtDescending()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userId = Guid.NewGuid();

            var author = new ApplicationUser
            {
                Id = userId,
                UserName = $"User_{uniqueSuffix}",
                NormalizedUserName = $"USER_{uniqueSuffix}",
                Email = $"u_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"U_{uniqueSuffix}@T.PL",
                FirstName = "Michał",
                LastName = "Pisarz"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Comp_{uniqueSuffix}",
                NIP = "123",
                OwnerId = userId,
                Owner = author
            };

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Jan",
                LastName = "Kowalski",
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = author,
                IsPrimary = true
            };

            var validNote = new ContactNote
            {
                Id = Guid.NewGuid(),
                Title = "Ważna notatka",
                Content = "Treść",
                ContactId = contact.Id,
                AuthorId = userId,
                Author = author,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            };

            var deletedNote = new ContactNote
            {
                Id = Guid.NewGuid(),
                Title = "Usunięta",
                Content = "Treść",
                ContactId = contact.Id,
                AuthorId = userId,
                Author = author,
                IsDeleted = true
            };

            var now = DateTime.UtcNow;

            var oldNote = new ContactNote
            {
                Id = Guid.NewGuid(),
                Title = "Old",
                Content = "C",
                ContactId = contact.Id,
                AuthorId = userId,
                Author = author,
                CreatedAt = now.AddDays(-5)
            };

            var newestNote = new ContactNote
            {
                Id = Guid.NewGuid(),
                Title = "Newest",
                Content = "C",
                ContactId = contact.Id,
                AuthorId = userId,
                Author = author,
                CreatedAt = now
            };

            var middleNote = new ContactNote
            {
                Id = Guid.NewGuid(),
                Title = "Middle",
                Content = "C",
                ContactId = contact.Id,
                AuthorId = userId,
                Author = author,
                CreatedAt = now.AddDays(-2)
            };

            _contextMock.Users.Add(author);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.Add(contact);
            _contextMock.Notes.AddRange(oldNote, middleNote, newestNote);
            await _contextMock.SaveChangesAsync();

            var command = new NoteListCommand
            {
                PageNumber = 1,
                PageSize = 10,
                searchId = contact.Id
            };

            // Act
            var result = await _contactServicesMock.GetContactNoteAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            var items = result.Data!.Items;

            await Assert.That(items).HasCount().EqualTo(3);

            await Assert.That(items[0].Id).IsEqualTo(newestNote.Id);
            await Assert.That(items[1].Id).IsEqualTo(middleNote.Id);
            await Assert.That(items[2].Id).IsEqualTo(oldNote.Id);
        }

        [Test]
        public async Task GetContactNoteAsync_ReturnsNotesOnlyForSpecificContact()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userId = Guid.NewGuid();

            var author = new ApplicationUser
            {
                Id = userId,
                UserName = $"User_{uniqueSuffix}",
                NormalizedUserName = $"USER_{uniqueSuffix}",
                Email = $"u_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"U_{uniqueSuffix}@T.PL",
                FirstName = "Michał",
                LastName = "Pisarz"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Company_{uniqueSuffix}",
                NIP = "111",
                OwnerId = userId,
                Owner = author
            };

            var targetContact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Target",
                LastName = "Contact",
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = author,
                IsPrimary = true
            };

            var otherContact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Other",
                LastName = "Contact",
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = author,
                IsPrimary = false
            };

            var targetNote = new ContactNote
            {
                Id = Guid.NewGuid(),
                Title = "Dla Target",
                Content = "Treść",
                ContactId = targetContact.Id,
                AuthorId = userId,
                Author = author
            };

            var otherNote = new ContactNote
            {
                Id = Guid.NewGuid(),
                Title = "Dla Other",
                Content = "Treść",
                ContactId = otherContact.Id,
                AuthorId = userId,
                Author = author
            };

            _contextMock.Users.Add(author);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.AddRange(targetContact, otherContact);
            _contextMock.Notes.AddRange(targetNote, otherNote);
            await _contextMock.SaveChangesAsync();

            var command = new NoteListCommand
            {
                PageNumber = 1,
                PageSize = 10,
                searchId = targetContact.Id
            };

            // Act 
            var result = await _contactServicesMock.GetContactNoteAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            var items = result.Data!.Items;

            await Assert.That(items).HasCount().EqualTo(1);
            await Assert.That(items.First().Id).IsEqualTo(targetNote.Id);
        }
    }
}
