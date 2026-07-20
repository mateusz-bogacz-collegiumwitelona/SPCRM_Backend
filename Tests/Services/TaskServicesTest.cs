using Domain.Enum;
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
    public class TaskServicesTest
    {
        protected AppDbContext _contextMock = null!;

        private static PostgreSqlContainer _dbContainer = null!;
        private static string _connectionString = null!;

        protected TaskServices _taskServicesMock = null!;
        protected ILogger<TaskServices> _loggerMock = null!;

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

            _loggerMock = new LoggerFactory().CreateLogger<TaskServices>();

            _taskServicesMock = new TaskServices(_contextMock, _loggerMock);
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

        // ─── GetTasksForCalendarAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetTasksForCalendarAsync_FiltersByDateRangeUserAndDeletedStatus()
        {
            // Arrange
            var targetUserId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var targetUser = new ApplicationUser
            {
                Id = targetUserId,
                UserName = $"U1_{uniqueSuffix}",
                Email = $"u1_{uniqueSuffix}@t.pl",
                FirstName = "Target",
                LastName = "User"
            };

            var otherUser = new ApplicationUser
            {
                Id = otherUserId,
                UserName = $"U2_{uniqueSuffix}",
                Email = $"u2_{uniqueSuffix}@t.pl",
                FirstName = "Other",
                LastName = "User"
            };

            var validTask = new Tasks
            {
                Id = Guid.NewGuid(),
                Title = "Poprawne",
                AssignedToId = targetUserId,
                AssignedTo = targetUser,
                DueAt = new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc),
                IsDeleted = false,
                Status = TaskStatusEnum.ToDo,
                Priority = TaskPriorityEnum.Medium,
                Description = "Zadanie poprawne do testu"
            };

            var outOfDateTask = new Tasks
            {
                Id = Guid.NewGuid(),
                Title = "Poza datą",
                AssignedToId = targetUserId,
                AssignedTo = targetUser,
                DueAt = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc),
                IsDeleted = false,
                Status = TaskStatusEnum.ToDo,
                Priority = TaskPriorityEnum.Medium,
                Description = "Zadanie poza zakresem dat do testu"
            };

            var deletedTask = new Tasks
            {
                Id = Guid.NewGuid(),
                Title = "Usunięte",
                AssignedToId = targetUserId,
                AssignedTo = targetUser,
                DueAt = new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc),
                IsDeleted = true,
                Status = TaskStatusEnum.ToDo,
                Priority = TaskPriorityEnum.Medium,
                Description = "Zadanie usunięte do testu"
            };

            var otherUserTask = new Tasks
            {
                Id = Guid.NewGuid(),
                Title = "Inny User",
                AssignedToId = otherUserId,
                AssignedTo = otherUser,
                DueAt = new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc),
                IsDeleted = false,
                Status = TaskStatusEnum.ToDo,
                Priority = TaskPriorityEnum.Medium,
                Description = "Zadanie przypisane do innego użytkownika do testu"
            };

            _contextMock.Users.AddRange(targetUser, otherUser);
            _contextMock.Tasks.AddRange(validTask, outOfDateTask, deletedTask, otherUserTask);
            await _contextMock.SaveChangesAsync();


            var command = new TaskCalendarCommand
            {
                DateFrom = new DateOnly(2026, 7, 10),
                DateTo = new DateOnly(2026, 7, 20),
                UserId = targetUserId
            };

            // Act
            var result = await _taskServicesMock.GetTasksForCalendarAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var tasks = result.Data!;

            await Assert.That(tasks).HasCount().EqualTo(1);
            await Assert.That(tasks[0].Id).IsEqualTo(validTask.Id);
        }

        [Test]
        public async Task GetTasksForCalendarAsync_HandlesNullRelationsGracefully()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var owner = new ApplicationUser
            {
                Id = userId,
                UserName = $"Owner_{uniqueSuffix}",
                Email = $"owner_{uniqueSuffix}@test.pl",
                FirstName = "Piotr",
                LastName = "Kowal"
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
                OwnerId = owner.Id,
                Owner = owner
            };

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Jan",
                LastName = "Kowalski",
                IsPrimary = true,
                OwnerId = owner.Id,
                Owner = owner,
                CompanyId = company.Id,
                Company = company
            };

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Super Transakcja",
                Value = 100,
                CompanyId = company.Id,
                Company = company,
                CurrencyId = currency.Id,
                Currency = currency,
                OwnerId = owner.Id,
                Owner = owner,
                CloseDate = DateTime.UtcNow
            };

            var taskWithRelations = new Tasks
            {
                Id = Guid.NewGuid(),
                Title = "Z relacjami",
                AssignedToId = userId,
                AssignedTo = owner,
                DueAt = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc),
                ContactId = contact.Id,
                Contact = contact,
                DealId = deal.Id,
                Deal = deal,
                Description = "Zadanie z relacjami do testu"
            };

            var taskWithoutRelations = new Tasks
            {
                Id = Guid.NewGuid(),
                Title = "Bez relacji",
                AssignedToId = userId,
                AssignedTo = owner,
                DueAt = new DateTime(2026, 5, 2, 10, 0, 0, DateTimeKind.Utc),
                ContactId = null,
                DealId = null,
                Description = "Zadanie bez relacji do testu"
            };

            _contextMock.Users.Add(owner);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.Add(contact);
            _contextMock.Deals.Add(deal);
            _contextMock.Tasks.AddRange(taskWithRelations, taskWithoutRelations);
            await _contextMock.SaveChangesAsync();


            var command = new TaskCalendarCommand
            {
                DateFrom = new DateOnly(2026, 1, 1),
                DateTo = new DateOnly(2026, 12, 31),
                UserId = userId
            };

            // Act
            var result = await _taskServicesMock.GetTasksForCalendarAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            var tasks = result.Data!;

            var mappedWithRelations = tasks.First(t => t.Id == taskWithRelations.Id);
            await Assert.That(mappedWithRelations.ContactFirstName).IsEqualTo("Jan");
            await Assert.That(mappedWithRelations.ContactLastName).IsEqualTo("Kowalski");
            await Assert.That(mappedWithRelations.DealName).IsEqualTo("Super Transakcja");

            var mappedWithoutRelations = tasks.First(t => t.Id == taskWithoutRelations.Id);
            await Assert.That(mappedWithoutRelations.ContactFirstName).IsEmpty();
            await Assert.That(mappedWithoutRelations.DealName).IsEmpty();
        }

        [Test]
        public async Task GetTasksForCalendarAsync_SortsTasksChronologically()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var user = new ApplicationUser
            {
                Id = userId,
                UserName = $"U_{uniqueSuffix}",
                Email = $"u_{uniqueSuffix}@t.pl",
                FirstName = "Test",
                LastName = "User"
            };

            var taskDay15 = new Tasks
            {
                Id = Guid.NewGuid(),
                Title = "Środek",
                AssignedToId = userId,
                AssignedTo = user,
                DueAt = new DateTime(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc),
                Description = "Zadanie w środku miesiąca"
            };

            var taskDay1 = new Tasks
            {
                Id = Guid.NewGuid(),
                Title = "Początek",
                AssignedToId = userId,
                AssignedTo = user,
                DueAt = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc),
                Description = "Zadanie na początku miesiąca"
            };

            var taskDay30 = new Tasks
            {
                Id = Guid.NewGuid(),
                Title = "Koniec",
                AssignedToId = userId,
                AssignedTo = user,
                DueAt = new DateTime(2026, 8, 30, 15, 0, 0, DateTimeKind.Utc),
                Description = "Zadanie na końcu miesiąca"
            };

            _contextMock.Users.Add(user);
            _contextMock.Tasks.AddRange(taskDay15, taskDay1, taskDay30);
            await _contextMock.SaveChangesAsync();

            var command = new TaskCalendarCommand
            {
                DateFrom = new DateOnly(2026, 8, 1),
                DateTo = new DateOnly(2026, 8, 31),
                UserId = userId
            };

            // Act
            var result = await _taskServicesMock.GetTasksForCalendarAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();

            var tasks = result.Data!.Where(t => t.Id == taskDay15.Id || t.Id == taskDay1.Id || t.Id == taskDay30.Id).ToList();

            await Assert.That(tasks).HasCount().EqualTo(3);
            await Assert.That(tasks[0].Id).IsEqualTo(taskDay1.Id);
            await Assert.That(tasks[1].Id).IsEqualTo(taskDay15.Id);
            await Assert.That(tasks[2].Id).IsEqualTo(taskDay30.Id);
        }

        // ─── GetTaskDictionariesAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetTaskDictionariesAsync_ReturnsCorrectDictionaries()
        {
            // Act
            var result = await _taskServicesMock.GetTaskDictionariesAsync();

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var type = result.Data!.GetType();

            var statusesProperty = type.GetProperty("Statuses");
            var prioritiesProperty = type.GetProperty("Priorities");

            await Assert.That(statusesProperty).IsNotNull();
            await Assert.That(prioritiesProperty).IsNotNull();

            var statuses = (System.Collections.IEnumerable)statusesProperty!.GetValue(result.Data)!;
            var priorities = (System.Collections.IEnumerable)prioritiesProperty!.GetValue(result.Data)!;

            int statusCount = 0;
            foreach (var item in statuses) statusCount++;

            var enumStatusCount = Enum.GetNames(typeof(TaskStatusEnum)).Length;
            await Assert.That(statusCount).IsEqualTo(enumStatusCount);

            int priorityCount = 0;
            foreach (var item in priorities) priorityCount++;

            var enumPriorityCount = Enum.GetNames(typeof(TaskPriorityEnum)).Length;
            await Assert.That(priorityCount).IsEqualTo(enumPriorityCount);
        }

        // ─── GetTaskDetailResponse ─────────────────────────────────────────────────

        [Test]
        public async Task GetTaskDetailResponse_WhenTaskExists_ReturnsCorrectDetails()
        {
            var userId = Guid.NewGuid();
            var taskId = Guid.NewGuid();
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var user = new ApplicationUser
            {
                Id = userId,
                UserName = $"Owner_{uniqueSuffix}",
                NormalizedUserName = $"OWNER_{uniqueSuffix}",
                Email = $"owner_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"OWNER_{uniqueSuffix}@TEST.PL",
                FirstName = "Piotr",
                LastName = "Kowal"
            };

            var task = new Tasks
            {
                Id = taskId,
                Title = "Testowe zadanie",
                Description = "Opis zadania",
                DueAt = DateTime.UtcNow,
                Status = TaskStatusEnum.InProgress,
                Priority = TaskPriorityEnum.High,
                AssignedToId = userId,
                AssignedTo = user
            };

            _contextMock.Users.Add(user);
            _contextMock.Tasks.Add(task);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _taskServicesMock.GetTaskDetailResponse(taskId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var data = result.Data!;
            await Assert.That(data.Id).IsEqualTo(taskId);
            await Assert.That(data.Title).IsEqualTo("Testowe zadanie");
            await Assert.That(data.Description).IsEqualTo("Opis zadania");
            await Assert.That(data.Status).IsEqualTo(TaskStatusEnum.InProgress.ToString());
            await Assert.That(data.Priority).IsEqualTo(TaskPriorityEnum.High.ToString());
        }

        [Test]
        public async Task GetTaskDetailResponse_WhenTaskDoesNotExist_Returns404()
        {
            // Act
            var result = await _taskServicesMock.GetTaskDetailResponse(Guid.NewGuid());

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.Message).IsEqualTo("Task not found");
        }

        // ─── GetTaskContactAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetTaskContactAsync_ReturnsContactDetailsWithValidWays()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var taskId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = userId,
                UserName = "User",
                Email = "u@t.pl",
                FirstName = "Jan",
                LastName = "Kowalski"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = "Firma Testowa",
                NIP = "123",
                OwnerId = userId,
                Owner = owner
            };

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Anna",
                LastName = "Nowak",
                JobTitle = "Manager",
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = owner,
                IsPrimary = true
            };

            // Detale kontaktu
            var detailPrimary = new ContactDetail
            {
                Id = Guid.NewGuid(),
                ContactId = contact.Id,
                Type = ContactDetailTypeEnum.EMAIL,
                Value = "anna@test.pl",
                IsPrimary = true,
                IsDeleted = false
            };

            var detailSecondary = new ContactDetail
            {
                Id = Guid.NewGuid(),
                ContactId = contact.Id,
                Type = ContactDetailTypeEnum.PHONE,
                Value = "123456789",
                IsPrimary = false,
                IsDeleted = false
            };

            var detailDeleted = new ContactDetail
            {
                Id = Guid.NewGuid(),
                ContactId = contact.Id,
                Type = ContactDetailTypeEnum.EMAIL,
                Value = "old@test.pl",
                IsDeleted = true
            };

            var task = new Tasks
            {
                Id = taskId,
                Title = "Zadanie",
                AssignedToId = userId,
                AssignedTo = owner,
                ContactId = contact.Id,
                Contact = contact,
                Description = "Zadanie z kontaktem",
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.Add(contact);
            _contextMock.ContactDetails.AddRange(detailPrimary, detailSecondary, detailDeleted);
            _contextMock.Tasks.Add(task);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _taskServicesMock.GetTaskContactAsync(taskId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var data = result.Data!;
            await Assert.That(data.FirstName).IsEqualTo("Anna");
            await Assert.That(data.CompanyName).IsEqualTo("Firma Testowa");

            await Assert.That(data.ContactWays).HasCount().EqualTo(2);
            await Assert.That(data.ContactWays.Any(c => c.Value == "123456789")).IsTrue();
            await Assert.That(data.ContactWays.Any(c => c.Value == "old@test.pl")).IsFalse();
        }

        [Test]
        public async Task GetTaskContactAsync_WhenContactMissingOrTaskNotFound_Returns404()
        {
            // Arrange
            var taskIdNoContact = Guid.NewGuid();
            var userId = Guid.NewGuid();

            var user = new ApplicationUser
            {
                Id = userId,
                UserName = "User",
                Email = "u@t.pl",
                FirstName = "Test",
                LastName = "Test",
            };

            _contextMock.Users.Add(user);

            var taskNoContact = new Tasks
            {
                Id = taskIdNoContact,
                Title = "Bez kontaktu",
                AssignedToId = userId,
                AssignedTo = user,
                ContactId = null,
                Description = "Zadanie bez kontaktu"
            };

            _contextMock.Tasks.Add(taskNoContact);
            await _contextMock.SaveChangesAsync();

            // Act
            var resultNoContact = await _taskServicesMock.GetTaskContactAsync(taskIdNoContact);

            // Assert
            await Assert.That(resultNoContact.IsSuccess).IsFalse();
            await Assert.That(resultNoContact.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);

            var resultNotFound = await _taskServicesMock.GetTaskContactAsync(Guid.NewGuid());
            await Assert.That(resultNotFound.IsSuccess).IsFalse();
            await Assert.That(resultNotFound.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
        }

        // ─── GetTaskDealAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetTaskDealAsync_ReturnsDealDetailsCorrectly()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var taskId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = userId,
                UserName = "User",
                Email = "u@t.pl",
                FirstName = "Jan",
                LastName = "Kowalski"
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Euro",
                Code = "EUR",
                DecimalPlaces = 2
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = "Firma",
                NIP = "123",
                OwnerId = userId,
                Owner = owner
            };

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Duży Projekt",
                Value = 500000,
                Status = DealsStatusEnum.Complete,
                CloseDate = DateTime.UtcNow,
                CompanyId = company.Id,
                Company = company,
                CurrencyId = currency.Id,
                Currency = currency,
                OwnerId = userId,
                Owner = owner
            };

            var task = new Tasks
            {
                Id = taskId,
                Title = "Zadanie",
                AssignedToId = userId,
                AssignedTo = owner,
                DealId = deal.Id,
                Deal = deal,
                Description = "Zadanie z dealem",
            };

            _contextMock.Users.Add(owner);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            _contextMock.Deals.Add(deal);
            _contextMock.Tasks.Add(task);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _taskServicesMock.GetTaskDealAsync(taskId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var data = result.Data!;
            await Assert.That(data.DealId).IsEqualTo(deal.Id);
            await Assert.That(data.Name).IsEqualTo("Duży Projekt");
            await Assert.That(data.Value).IsEqualTo(500000);
            await Assert.That(data.Status).IsEqualTo(DealsStatusEnum.Complete.ToString());
            await Assert.That(data.CurrencyCode).IsEqualTo("EUR");
            await Assert.That(data.DecimalPlaces).IsEqualTo(2);
        }

        [Test]
        public async Task GetTaskDealAsync_WhenDealMissingOrTaskNotFound_Returns404()
        {
            // Arrange
            var taskIdNoDeal = Guid.NewGuid();
            var userId = Guid.NewGuid();

            var user = new ApplicationUser
            {
                Id = userId,
                UserName = "TestUser",
                Email = "test@test.pl",
                FirstName = "Test",
                LastName = "User"
            };
            _contextMock.Users.Add(user);

            var taskNoDeal = new Tasks
            {
                Id = taskIdNoDeal,
                Title = "Bez deala",
                AssignedToId = userId,
                AssignedTo = user,
                DealId = null,
                Description = "Zadanie bez deala"
            };

            _contextMock.Tasks.Add(taskNoDeal);
            await _contextMock.SaveChangesAsync();

            // Act
            var resultNoDeal = await _taskServicesMock.GetTaskDealAsync(taskIdNoDeal);

            // Assert
            await Assert.That(resultNoDeal.IsSuccess).IsFalse();
            await Assert.That(resultNoDeal.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);

            var resultNotFound = await _taskServicesMock.GetTaskDealAsync(Guid.NewGuid());
            await Assert.That(resultNotFound.IsSuccess).IsFalse();
            await Assert.That(resultNotFound.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
        }


        // ─── GetTaskNotesAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetTaskNotesAsync_WhenTaskDoesNotExist_Returns404()
        {
            // Act
            var result = await _taskServicesMock.GetTaskNotesAsync(Guid.NewGuid());

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

            // Act
            var result = await _taskServicesMock.GetTaskNotesAsync(taskId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            var notes = result.Data!;

            await Assert.That(notes).HasCount().EqualTo(2);
            await Assert.That(notes[0].NoteId).IsEqualTo(note2.Id);
            await Assert.That(notes[0].AuthorFirstName).IsEqualTo("Anna");
            await Assert.That(notes[1].NoteId).IsEqualTo(note1.Id);
        }
    }
}
