using Domain.Common;
using Domain.Constants;
using Domain.Exceptions.Exception;
using Domain.Models;
using Infrastructure;
using Infrastructure.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Services.Command.Note;
using Services.Services;
using Testcontainers.PostgreSql;

namespace Tests.Services
{

    public class NoteServiceTest
    {
        protected AppDbContext _contextMock = null!;

        private static PostgreSqlContainer _dbContainer = null!;
        private static string _connectionString = null!;

        protected NoteServices _noteServicesMock = null!;
        protected ILogger<NoteServices> _loggerMock = null!;
        protected UserManager<ApplicationUser> _userManagerMock = null!;
        protected RoleManager<IdentityRole<Guid>> _roleManagerMock = null!;

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

            var userStore = new UserStore<ApplicationUser, IdentityRole<Guid>, AppDbContext, Guid>(_contextMock);
            var passwordHasher = new PasswordHasher<ApplicationUser>();
            var normalizer = new UpperInvariantLookupNormalizer();
            var userManagerLogger = NullLogger<UserManager<ApplicationUser>>.Instance;
            var identityOptions = Options.Create(new IdentityOptions());

            _userManagerMock = new UserManager<ApplicationUser>(
                userStore,
                identityOptions,
                passwordHasher,
                null!,
                null!,
                normalizer,
                new IdentityErrorDescriber(),
                null!,
                userManagerLogger
                );

            var roleStore = new RoleStore<IdentityRole<Guid>, AppDbContext, Guid>(_contextMock);
            _roleManagerMock = new RoleManager<IdentityRole<Guid>>(
                roleStore,
                null!,
                normalizer,
                null!,
                null!
                );

            _loggerMock = new LoggerFactory().CreateLogger<NoteServices>();

            _noteServicesMock = new NoteServices(_contextMock, _loggerMock, _userManagerMock);
        }

        [After(Test)]
        public async Task CleanupAsync()
        {
            await _contextMock.DisposeAsync();

            _userManagerMock?.Dispose();
            _roleManagerMock?.Dispose();

            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DROP SCHEMA IF EXISTS {_currentSchema} CASCADE;";
            await cmd.ExecuteNonQueryAsync();
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
                SearchId = contact.Id
            };

            // Act
            var result = await _noteServicesMock.GetContactNoteAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var items = result.Data!.Items;

            await Assert.That(items).Count().IsEqualTo(1);

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

            oldNote.CreatedAt = now.AddDays(-5);
            middleNote.CreatedAt = now.AddDays(-2);
            newestNote.CreatedAt = now;

            _contextMock.Notes.UpdateRange(oldNote, middleNote, newestNote);
            await _contextMock.SaveChangesAsync();

            var command = new NoteListCommand
            {
                PageNumber = 1,
                PageSize = 10,
                SearchId = contact.Id
            };

            // Act
            var result = await _noteServicesMock.GetContactNoteAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            var items = result.Data!.Items;

            await Assert.That(items).Count().IsEqualTo(3);

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
                SearchId = targetContact.Id
            };

            // Act 
            var result = await _noteServicesMock.GetContactNoteAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            var items = result.Data!.Items;

            await Assert.That(items).Count().IsEqualTo(1);
            await Assert.That(items.First().Id).IsEqualTo(targetNote.Id);
        }


        // ─── GetTaskNotesAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetTaskNotesAsync_WhenTaskDoesNotExist_Returns404()
        {
            // Act
            var result = await _noteServicesMock.GetTaskNotesAsync(Guid.NewGuid());

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.Message).IsEqualTo("Task for this note not found");
        }

        [Test]
        public async Task GetTaskNotesAsync_ReturnsSortedValidNotes()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var taskId = Guid.NewGuid();

            var user = new ApplicationUser
            {
                Id = userId,
                UserName = "User",
                Email = "u@t.pl",
                FirstName = "Anna",
                LastName = "Nowak"
            };

            var task = new Tasks
            {
                Id = taskId,
                Title = "Zadanie",
                AssignedToId = userId,
                AssignedTo = user,
                Description = "Description",
            };

            var now = DateTime.UtcNow;

            var note1 = new TaskNote
            {
                Id = Guid.NewGuid(),
                Title = "Stara",
                Content = "Duppppa",
                TaskId = taskId,
                AuthorId = userId,
                Author = user,
                CreatedAt = now.AddDays(-5),
                IsDeleted = false
            };

            var note2 = new TaskNote
            {
                Id = Guid.NewGuid(),
                Title = "Nowa",
                Content = "Dupa",
                TaskId = taskId,
                AuthorId = userId,
                Author = user,
                CreatedAt = now,
                IsDeleted = false
            };

            var noteDeleted = new TaskNote
            {
                Id = Guid.NewGuid(),
                Title = "Usunięta",
                Content = "Dupa",
                TaskId = taskId,
                AuthorId = userId,
                Author = user,
                IsDeleted = true
            };

            _contextMock.Users.Add(user);
            _contextMock.Tasks.Add(task);
            _contextMock.Notes.AddRange(note1, note2, noteDeleted);
            await _contextMock.SaveChangesAsync();

            note1.CreatedAt = now.AddDays(-5);
            note2.CreatedAt = now;

            _contextMock.Notes.UpdateRange(note1, note2);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _noteServicesMock.GetTaskNotesAsync(taskId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            var notes = result.Data!;

            await Assert.That(notes).Count().IsEqualTo(2);
            await Assert.That(notes[0].NoteId).IsEqualTo(note2.Id);
            await Assert.That(notes[0].AuthorFirstName).IsEqualTo("Anna");
            await Assert.That(notes[1].NoteId).IsEqualTo(note1.Id);
        }

        // ─── GetDealNotesAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetDealNotesAsync_FiltersDeletedAndOtherTypes_AndMapsCorrectly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userId = Guid.NewGuid();

            var author = new ApplicationUser
            {
                Id = userId,
                UserName = $"U_{uniqueSuffix}",
                NormalizedUserName = $"U_{uniqueSuffix}",
                Email = $"e_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"E_{uniqueSuffix}@T.PL",
                FirstName = "Anna",
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
                Name = $"Comp_{uniqueSuffix}",
                NIP = "111",
                OwnerId = userId,
                Owner = author
            };

            var targetDeal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Target Deal",
                Value = 0,
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = author,
                CurrencyId = currency.Id,
                Currency = currency
            };

            var otherDeal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Other Deal",
                Value = 0,
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = author,
                CurrencyId = currency.Id,
                Currency = currency
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
                IsPrimary = true,
            };

            var validNote = new DealNote
            {
                Id = Guid.NewGuid(),
                Title = "Valid Note",
                Content = "Treść",
                DealId = targetDeal.Id,
                AuthorId = userId,
                Author = author,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            };

            var deletedNote = new DealNote
            {
                Id = Guid.NewGuid(),
                Title = "Deleted Note",
                Content = "Treść",
                DealId = targetDeal.Id,
                AuthorId = userId,
                Author = author,
                IsDeleted = true,
                CreatedAt = DateTime.UtcNow
            };

            var otherDealNote = new DealNote
            {
                Id = Guid.NewGuid(),
                Title = "Other Deal Note",
                Content = "Treść",
                DealId = otherDeal.Id,
                AuthorId = userId,
                Author = author,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            };

            var contactNote = new ContactNote
            {
                Id = Guid.NewGuid(),
                Title = "Contact Note",
                Content = "Treść",
                ContactId = contact.Id,
                AuthorId = userId,
                Author = author,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            };

            _contextMock.Users.Add(author);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            _contextMock.Deals.AddRange(targetDeal, otherDeal);
            _contextMock.Contacts.Add(contact);
            _contextMock.Notes.AddRange(validNote, deletedNote, otherDealNote, contactNote);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _noteServicesMock.GetDealNotesAsync(targetDeal.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var items = result.Data!;

            await Assert.That(items).Count().IsEqualTo(1);

            var mappedNote = items.First();
            await Assert.That(mappedNote.NoteId).IsEqualTo(validNote.Id);
            await Assert.That(mappedNote.Title).IsEqualTo("Valid Note");
            await Assert.That(mappedNote.AuthorFirstName).IsEqualTo("Anna");
            await Assert.That(mappedNote.AuthorLastName).IsEqualTo("Nowak");
        }

        [Test]
        public async Task GetDealNotesAsync_SortsNotesByCreatedAtDescending()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userId = Guid.NewGuid();

            var author = new ApplicationUser
            {
                Id = userId,
                UserName = $"U2_{uniqueSuffix}",
                NormalizedUserName = $"U2_{uniqueSuffix}",
                Email = $"e2_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"E2_{uniqueSuffix}@T.PL",
                FirstName = "Anna",
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
                Name = $"C2_{uniqueSuffix}",
                NIP = "222",
                OwnerId = userId,
                Owner = author
            };

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Deal",
                Value = 0,
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = author,
                CurrencyId = currency.Id,
                Currency = currency
            };

            var now = DateTime.UtcNow;

            var oldNote = new DealNote
            {
                Id = Guid.NewGuid(),
                Title = "Old",
                Content = "...",
                DealId = deal.Id,
                AuthorId = userId,
                Author = author,
                CreatedAt = now.AddDays(-5)
            };

            var newestNote = new DealNote
            {
                Id = Guid.NewGuid(),
                Title = "Newest",
                Content = "...",
                DealId = deal.Id,
                AuthorId = userId,
                Author = author,
                CreatedAt = now
            };

            var middleNote = new DealNote
            {
                Id = Guid.NewGuid(),
                Title = "Middle",
                Content = "...",
                DealId = deal.Id,
                AuthorId = userId,
                Author = author,
                CreatedAt = now.AddDays(-2)
            };

            _contextMock.Users.Add(author);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            _contextMock.Deals.Add(deal);
            _contextMock.Notes.AddRange(oldNote, middleNote, newestNote);
            await _contextMock.SaveChangesAsync();

            oldNote.CreatedAt = now.AddDays(-5);
            middleNote.CreatedAt = now.AddDays(-2);
            newestNote.CreatedAt = now;

            _contextMock.Notes.UpdateRange(oldNote, middleNote, newestNote);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _noteServicesMock.GetDealNotesAsync(deal.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var items = result.Data!;

            await Assert.That(items).Count().IsEqualTo(3);
            await Assert.That(items[0].NoteId).IsEqualTo(newestNote.Id);
            await Assert.That(items[1].NoteId).IsEqualTo(middleNote.Id);
            await Assert.That(items[2].NoteId).IsEqualTo(oldNote.Id);
        }

        [Test]
        public async Task GetDealNotesAsync_WhenNoNotesExist_ReturnsEmptyListWithSuccessStatus()
        {
            // Arrange
            var randomDealId = Guid.NewGuid();

            // Act
            var result = await _noteServicesMock.GetDealNotesAsync(randomDealId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!).IsEmpty();
        }

        // ─── EditNoteAsync ─────────────────────────────────────────────────

        [Test]
        public async Task EditNoteAsync_WhenNoteDoesNotExist_Returns404()
        {
            // Arrange
            var command = new NoteEditCommand
            {
                Id = Guid.NewGuid(),
                Title = "New Title",
                Content = "New Content"
            };

            // Act
            var result = await _noteServicesMock.EditNoteAsync(command, Guid.NewGuid());

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.Message).IsEqualTo("Note not found");
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.NoteNotFound);
        }

        [Test]
        public async Task EditNoteAsync_WhenUserDoesNotExist_ThrowsUserNotFoundException()
        {
            // Arrange
            Guid ownerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                FirstName = "Anna",
                LastName = "Nowak",
                UserName = "user1",
                Email = "anna.nowak@example.com"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = "Test Company",
                NIP = "1233211231",
                OwnerId = ownerId,
                Owner = owner
            };

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Jan",
                LastName = "Kowalski",
                CompanyId = company.Id,
                Company = company,
                IsPrimary = true,
                Owner = owner,
                OwnerId = ownerId
            };

            var note = new ContactNote
            {
                Id = Guid.NewGuid(),
                Title = "Old Title",
                Content = "Old Content",
                Contact = contact,
                ContactId = contact.Id,
                Author = owner,
                AuthorId = ownerId
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.Add(contact);
            _contextMock.Notes.Add(note);
            await _contextMock.SaveChangesAsync();

            var command = new NoteEditCommand
            {
                Id = note.Id,
                Title = "New Title",
                Content = "New Content"
            };

            // Act & Assert
            await Assert.That(async () => await _noteServicesMock.EditNoteAsync(command, Guid.NewGuid()))
                .Throws<UserNotFoundException>();
        }

        [Test]
        public async Task EditNoteAsync_WhenUserNotHaveAccess_ThrowsForbiddenException()
        {
            // Arrange
            Guid ownerId = Guid.NewGuid();
            Guid otherUserId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                FirstName = "Anna",
                LastName = "Nowak",
                UserName = "user1",
                Email = "anna.nowak@example.com"
            };

            var otherUser = new ApplicationUser
            {
                Id = otherUserId,
                FirstName = "Piotr",
                LastName = "Kowalski",
                UserName = "user2",
                Email = "piotr.kowalski@example.com"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = "Test Company",
                NIP = "1233211231",
                OwnerId = ownerId,
                Owner = owner
            };

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Jan",
                LastName = "Kowalski",
                CompanyId = company.Id,
                Company = company,
                IsPrimary = true,
                Owner = owner,
                OwnerId = ownerId
            };

            var note = new ContactNote
            {
                Id = Guid.NewGuid(),
                Title = "Old Title",
                Content = "Old Content",
                Contact = contact,
                ContactId = contact.Id,
                Author = owner,
                AuthorId = ownerId
            };

            _contextMock.Users.AddRange(owner, otherUser);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.Add(contact);
            _contextMock.Notes.Add(note);
            await _contextMock.SaveChangesAsync();

            var command = new NoteEditCommand
            {
                Id = note.Id,
                Title = "New Title",
                Content = "New Content"
            };

            // Act & Assert
            await Assert.That(async () => await _noteServicesMock.EditNoteAsync(command, otherUserId))
                .Throws<ForbiddenException>();
        }

        [Test]
        public async Task EditNoteAsync_WhenEverythingIsValid_UpdatesNoteSuccessfully()
        {
            // Arrange
            Guid ownerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                FirstName = "Anna",
                LastName = "Nowak",
                UserName = "user1",
                Email = "anna.nowak@example.com"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = "Test Company",
                NIP = "1233211231",
                OwnerId = ownerId,
                Owner = owner
            };

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Jan",
                LastName = "Kowalski",
                CompanyId = Guid.NewGuid(),
                IsPrimary = true,
                Owner = owner,
                Company = company
            };

            var note = new ContactNote
            {
                Id = Guid.NewGuid(),
                Title = "Old Title",
                Content = "Old Content",
                Contact = contact,
                Author = owner
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.Add(contact);
            _contextMock.Notes.Add(note);
            await _contextMock.SaveChangesAsync();

            var command = new NoteEditCommand
            {
                Id = note.Id,
                Title = "New Title",
                Content = "New Content"
            };

            // Act
            var result = await _noteServicesMock.EditNoteAsync(command, ownerId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("Note updated successfully");

        }

        // ─── AddNoteAsync ──────────────────────────────────────────────────────

        [Test]
        public async Task AddNoteAsync_WhenUserDoesNotExist_ThrowsUserNotFoundException()
        {
            // Arrange
            var command = new NoteAddCommand
            {
                Title = "Tytuł",
                Content = "Treść",
                TargetId = Guid.NewGuid(),
                NoteType = NoteEnum.Contact,
                AuthorId = Guid.NewGuid()
            };

            // Act & Assert
            await Assert.That(async () => await _noteServicesMock.AddNoteAsync(command))
                .Throws<UserNotFoundException>();
        }

        [Test]
        public async Task AddNoteAsync_WhenTargetDoesNotExist_Returns404()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new ApplicationUser
            {
                Id = userId,
                UserName = "testuser",
                Email = "test@example.com",
                FirstName = "Jan",
                LastName = "Kowalski"
            };

            _contextMock.Users.Add(user);
            await _contextMock.SaveChangesAsync();

            var command = new NoteAddCommand
            {
                Title = "Tytuł",
                Content = "Treść",
                TargetId = Guid.NewGuid(),
                NoteType = NoteEnum.Contact,
                AuthorId = userId
            };

            // Act
            var result = await _noteServicesMock.AddNoteAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.Message).IsEqualTo($"{NoteEnum.Contact} for this note not found");
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.NoteTargetNotFound);
        }

        [Test]
        public async Task AddNoteAsync_WhenValidContact_AddsNoteSuccessfully()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var contactId = Guid.NewGuid();

            var user = new ApplicationUser
            {
                Id = userId,
                UserName = "testuser",
                Email = "test@example.com",
                FirstName = "Jan",
                LastName = "Kowalski"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = "Firma Testowa",
                NIP = "1234567890",
                OwnerId = userId,
                Owner = user
            };

            var contact = new Contact
            {
                Id = contactId,
                FirstName = "Piotr",
                LastName = "Nowak",
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = user,
                IsPrimary = true
            };

            _contextMock.Users.Add(user);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.Add(contact);
            await _contextMock.SaveChangesAsync();

            var command = new NoteAddCommand
            {
                Title = "Ważna notatka",
                Content = "To jest treść notatki do kontaktu",
                TargetId = contactId,
                NoteType = NoteEnum.Contact,
                AuthorId = userId
            };

            // Act
            var result = await _noteServicesMock.AddNoteAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status201Created);

            var savedNote = await _contextMock.Notes
                .OfType<ContactNote>()
                .FirstOrDefaultAsync(n => n.ContactId == contactId);

            await Assert.That(savedNote).IsNotNull();
            await Assert.That(savedNote!.Title).IsEqualTo("Ważna notatka");
            await Assert.That(savedNote.Content).IsEqualTo("To jest treść notatki do kontaktu");
            await Assert.That(savedNote.AuthorId).IsEqualTo(userId);
        }

        [Test]
        public async Task AddNoteAsync_WhenInvalidNoteType_Returns404()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new ApplicationUser
            {
                Id = userId,
                UserName = "testuser",
                Email = "test@example.com",
                FirstName = "Jan",
                LastName = "Kowalski"
            };

            _contextMock.Users.Add(user);
            await _contextMock.SaveChangesAsync();

            var command = new NoteAddCommand
            {
                Title = "Tytuł",
                Content = "Treść",
                TargetId = Guid.NewGuid(),
                NoteType = (NoteEnum)999,
                AuthorId = userId
            };

            // Act
            var result = await _noteServicesMock.AddNoteAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);
        }

        // ─── DeleteNoteAsync ──────────────────────────────────────────────────────

        [Test]
        public async Task DeleteNoteAsync_WhenNoteDoesNotExist_Return404()
        {
            // Act
            var result = await _noteServicesMock.DeleteNoteAsync(Guid.NewGuid(), Guid.NewGuid());

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.Message).IsEqualTo("Note not found or is already deleted");
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.NoteNotFound);
        }

        [Test]
        public async Task DeleteNoteAsync_WhenUserDoesNotExist_ThrowsUserNotFoundException()
        {
            // Arrange 
            Guid ownerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                FirstName = "Anna4",
                LastName = "Nowak4",
                UserName = "user14",
                Email = "anna.nowak4@example.com"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = "Test Company",
                NIP = "1233211231",
                OwnerId = ownerId,
                Owner = owner
            };

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Jan",
                LastName = "Kowalski",
                CompanyId = company.Id,
                Company = company,
                IsPrimary = true,
                Owner = owner,
                OwnerId = ownerId
            };

            var note = new ContactNote
            {
                Id = Guid.NewGuid(),
                Title = "Old Title",
                Content = "Old Content",
                Contact = contact,
                ContactId = contact.Id,
                Author = owner,
                AuthorId = ownerId
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.Add(contact);
            _contextMock.Notes.Add(note);
            await _contextMock.SaveChangesAsync();

            // Act & Assert
            await Assert.That(async () => await _noteServicesMock.DeleteNoteAsync(note.Id, Guid.NewGuid()))
                .Throws<UserNotFoundException>();
        }

        [Test]
        public async Task DeleteNoteAsync_WhenUserNotHaveAccess_ThrowsForbiddenException()
        {
            // Arrange 
            Guid ownerId = Guid.NewGuid();
            Guid otherUserId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                FirstName = "Anna",
                LastName = "Nowak",
                UserName = "user1",
                Email = "anna.nowak@example.com"
            };

            var otherUser = new ApplicationUser
            {
                Id = otherUserId,
                FirstName = "Anna3",
                LastName = "Nowak3",
                UserName = "user13",
                Email = "anna.nowak3@example.com"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = "Test Company",
                NIP = "1233211231",
                OwnerId = ownerId,
                Owner = owner
            };

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Jan",
                LastName = "Kowalski",
                CompanyId = company.Id,
                Company = company,
                IsPrimary = true,
                Owner = owner,
                OwnerId = ownerId
            };

            var note = new ContactNote
            {
                Id = Guid.NewGuid(),
                Title = "Old Title",
                Content = "Old Content",
                Contact = contact,
                ContactId = contact.Id,
                Author = owner,
                AuthorId = ownerId
            };

            await _userManagerMock.CreateAsync(owner, "Password123!");
            await _userManagerMock.CreateAsync(otherUser, "Password123!");
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.Add(contact);
            _contextMock.Notes.Add(note);
            await _contextMock.SaveChangesAsync();

            // Act & Assert
            await Assert.That(async () => await _noteServicesMock.DeleteNoteAsync(note.Id, otherUserId))
                .Throws<ForbiddenException>();
        }

        [Test]
        public async Task DeleteNoteAsync_WhenUserIsAuthor_DeleteSuccessfully()
        {
            Guid ownerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                FirstName = "Anna44",
                LastName = "Nowak44",
                UserName = "user14",
                Email = "anna.nowak44@example.com"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = "Test Company",
                NIP = "1233211231",
                OwnerId = ownerId,
                Owner = owner
            };

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Jan",
                LastName = "Kowalski",
                CompanyId = Guid.NewGuid(),
                IsPrimary = true,
                Owner = owner,
                Company = company
            };

            var note = new ContactNote
            {
                Id = Guid.NewGuid(),
                Title = "Old Title",
                Content = "Old Content",
                Contact = contact,
                Author = owner
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.Add(contact);
            _contextMock.Notes.Add(note);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _noteServicesMock.DeleteNoteAsync(note.Id, ownerId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("Note deleted successfully");
        }

        [Test]
        public async Task DeleteNoteAsync_WhenUserIsManager_DeleteSuccessfully()
        {
            // Arrange 
            Guid ownerId = Guid.NewGuid();
            Guid managerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                FirstName = "Anna",
                LastName = "Nowak",
                UserName = "user145",
                Email = "anna.nowa33k@example.com"
            };

            var manager = new ApplicationUser
            {
                Id = managerId,
                FirstName = "Anna34343",
                LastName = "Nowak334343",
                UserName = "user134334",
                Email = "anna.nowak433433@example.com"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = "Test Company",
                NIP = "1233211231",
                OwnerId = ownerId,
                Owner = owner
            };

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Jan",
                LastName = "Kowalski",
                CompanyId = Guid.NewGuid(),
                IsPrimary = true,
                Owner = owner,
                Company = company
            };

            var note = new ContactNote
            {
                Id = Guid.NewGuid(),
                Title = "Old Title",
                Content = "Old Content",
                Contact = contact,
                Author = owner
            };

            await _userManagerMock.CreateAsync(manager, "Password123!");
            await _userManagerMock.CreateAsync(owner, "Password123!");
            await _roleManagerMock.CreateAsync(new IdentityRole<Guid> { Name = "Manager", NormalizedName = "MANAGER" });
            await _userManagerMock.AddToRoleAsync(manager, "Manager");
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.Add(contact);
            _contextMock.Notes.Add(note);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _noteServicesMock.DeleteNoteAsync(note.Id, managerId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("Note deleted successfully");
        }
    }
}
