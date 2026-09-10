using Domain.Enum;
using Domain.Exceptions.Exception;
using Domain.Models;
using Infrastructure;
using Infrastructure.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Services.Command.Task;
using Services.Interfaces;
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
        protected IEntityAuthorizationService _entityAuthMock = null!;

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

            _loggerMock = new LoggerFactory().CreateLogger<TaskServices>();

            _entityAuthMock = new EntityAuthorizationService(_contextMock);

            _taskServicesMock = new TaskServices(_contextMock, _loggerMock, _entityAuthMock);
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

            await Assert.That(tasks).Count().IsEqualTo(1);
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

            await Assert.That(tasks).Count().IsEqualTo(3);
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
            await Assert.That(result.Message).IsEqualTo("Task not found.");
        }

        [Test]
        public async Task GetTaskDetailResponse_WhenTaskHasEmptyTitle_ThrowsDataCorruptionException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var taskId = Guid.NewGuid();
            var user = new ApplicationUser
            {
                Id = userId,
                UserName = "User",
                Email = "u@t.pl",
                FirstName = "Test",
                LastName = "User"
            };

            var task = new Tasks
            {
                Id = taskId,
                Title = "",
                AssignedToId = userId,
                AssignedTo = user,
                DueAt = DateTime.UtcNow,
                Description = "Zadanie z pustym tytułem do testu",
            };

            _contextMock.Users.Add(user);
            _contextMock.Tasks.Add(task);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            // Act & Assert
            await Assert.That(async () => await _taskServicesMock.GetTaskDetailResponse(taskId))
                .Throws<DataCorruptionException>();
        }

        [Test]
        public async Task GetTaskDealAsync_WhenDealHasNegativeValue_ThrowsDataCorruptionException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var taskId = Guid.NewGuid();

            var user = new ApplicationUser
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
                Name = "PLN",
                Code = "PLN",
                DecimalPlaces = 2
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = "Firma",
                NIP = "123",
                OwnerId = userId,
                Owner = user
            };

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Zły deal",
                Value = -100,
                CompanyId = company.Id,
                Company = company,
                CurrencyId = currency.Id,
                Currency = currency,
                OwnerId = userId,
                Owner = user,
                CloseDate = DateTime.UtcNow
            };

            var task = new Tasks
            {
                Id = taskId,
                Title = "Zadanie",
                AssignedToId = userId,
                AssignedTo = user,
                DealId = deal.Id,
                Deal = deal,
                Description = "Zadanie"
            };

            _contextMock.Users.Add(user);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            _contextMock.Deals.Add(deal);
            _contextMock.Tasks.Add(task);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            // Act & Assert
            await Assert.That(async () => await _taskServicesMock.GetTaskDealAsync(taskId))
                .Throws<DataCorruptionException>();
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

            await Assert.That(data.ContactWays).Count().IsEqualTo(2);
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

        [Test]
        public async Task GetTaskContactAsync_WhenContactHasEmptyFirstName_ThrowsDataCorruptionException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var taskId = Guid.NewGuid();
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var owner = new ApplicationUser
            {
                Id = userId,
                UserName = $"User_{uniqueSuffix}",
                Email = $"user_{uniqueSuffix}@test.pl",
                FirstName = "Jan",
                LastName = "Kowalski"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = "Firma Testowa",
                NIP = "1234567890",
                OwnerId = userId,
                Owner = owner
            };

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "   ",
                LastName = "Nowak",
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = owner,
                IsPrimary = true
            };

            var task = new Tasks
            {
                Id = taskId,
                Title = "Zadanie ze skażonym kontaktem",
                AssignedToId = userId,
                AssignedTo = owner,
                ContactId = contact.Id,
                Contact = contact,
                Description = "Opis"
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.Add(contact);
            _contextMock.Tasks.Add(task);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            // Act & Assert
            await Assert.That(async () => await _taskServicesMock.GetTaskContactAsync(taskId))
                .Throws<DataCorruptionException>();
        }

        [Test]
        public async Task GetTaskContactAsync_WhenContactHasMissingCompanyRelation_ThrowsDataCorruptionException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var taskId = Guid.NewGuid();
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var owner = new ApplicationUser
            {
                Id = userId,
                UserName = $"User_{uniqueSuffix}",
                Email = $"user_{uniqueSuffix}@test.pl",
                FirstName = "Jan",
                LastName = "Kowalski"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = "Firma Do Usunięcia",
                NIP = "1234567890",
                OwnerId = userId,
                Owner = owner
            };

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Anna",
                LastName = "Nowak",
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = owner,
                IsPrimary = true
            };

            var task = new Tasks
            {
                Id = taskId,
                Title = "Zadanie z kontaktem bez firmy",
                AssignedToId = userId,
                AssignedTo = owner,
                ContactId = contact.Id,
                Contact = contact,
                Description = "Opis"
            };

            _contextMock.Users.Add(owner);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.Add(contact);
            _contextMock.Tasks.Add(task);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            await _contextMock.Database.ExecuteSqlRawAsync(@"
                SET session_replication_role = 'replica';
                DELETE FROM ""Companies"";
                SET session_replication_role = 'origin';
            ");

            // Act & Assert
            await Assert.That(async () => await _taskServicesMock.GetTaskContactAsync(taskId))
                .Throws<DataCorruptionException>();
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

        [Test]
        public async Task GetTaskDealAsync_WhenDealHasMissingCurrencyRelation_ThrowsDataCorruptionException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var taskId = Guid.NewGuid();
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var owner = new ApplicationUser
            {
                Id = userId,
                UserName = $"User_{uniqueSuffix}",
                Email = $"user_{uniqueSuffix}@test.pl",
                FirstName = "Tomasz",
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
                Name = "Firma Handlowa",
                NIP = "9876543210",
                OwnerId = userId,
                Owner = owner
            };

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Deal bez waluty",
                Value = 50000,
                Status = DealsStatusEnum.InProgress,
                CloseDate = DateTime.UtcNow,
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = owner,
                CurrencyId = currency.Id,
                Currency = currency
            };

            var task = new Tasks
            {
                Id = taskId,
                Title = "Zadanie z dealem bez waluty",
                AssignedToId = userId,
                AssignedTo = owner,
                DealId = deal.Id,
                Deal = deal,
                Description = "Opis"
            };

            _contextMock.Users.Add(owner);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            _contextMock.Deals.Add(deal);
            _contextMock.Tasks.Add(task);
            await _contextMock.SaveChangesAsync();
            _contextMock.ChangeTracker.Clear();

            await _contextMock.Database.ExecuteSqlRawAsync(@"
                SET session_replication_role = 'replica';
                DELETE FROM ""Currencies"";
                SET session_replication_role = 'origin';
            ");

            // Act & Assert
            await Assert.That(async () => await _taskServicesMock.GetTaskDealAsync(taskId))
                .Throws<DataCorruptionException>();
        }

        // ─── GetUserTasksAsync ──────────────────────────────────────────────────────

        [Test]
        public async Task GetUserTasksAsync_FiltersByUserAndSoftDelete()
        {
            // Arrange
            var targetUserId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var targetUser = new ApplicationUser
            {
                Id = targetUserId,
                UserName = $"Target_{uniqueSuffix}",
                Email = $"target_{uniqueSuffix}@t.pl",
                FirstName = "Piotr",
                LastName = "Kowalski"
            };

            var otherUser = new ApplicationUser
            {
                Id = otherUserId,
                UserName = $"Other_{uniqueSuffix}",
                Email = $"other_{uniqueSuffix}@t.pl",
                FirstName = "Marek",
                LastName = "Nowak"
            };

            var validTask = new Tasks
            {
                Id = Guid.NewGuid(),
                Title = "Zadanie target usera",
                AssignedToId = targetUserId,
                Status = TaskStatusEnum.ToDo,
                Priority = TaskPriorityEnum.Medium,
                DueAt = DateTime.UtcNow.AddDays(2),
                Description = "Opis",
                IsDeleted = false
            };

            var deletedTask = new Tasks
            {
                Id = Guid.NewGuid(),
                Title = "Usunięte zadanie",
                AssignedToId = targetUserId,
                Status = TaskStatusEnum.ToDo,
                Priority = TaskPriorityEnum.Low,
                DueAt = DateTime.UtcNow.AddDays(1),
                Description = "Opis",
                IsDeleted = true
            };

            var otherUserTask = new Tasks
            {
                Id = Guid.NewGuid(),
                Title = "Zadanie innego usera",
                AssignedToId = otherUserId,
                Status = TaskStatusEnum.ToDo,
                Priority = TaskPriorityEnum.High,
                DueAt = DateTime.UtcNow.AddDays(3),
                Description = "Opis",
                IsDeleted = false
            };

            _contextMock.Users.AddRange(targetUser, otherUser);
            _contextMock.Tasks.AddRange(validTask, deletedTask, otherUserTask);
            await _contextMock.SaveChangesAsync();

            var command = new UserTaskListCommand
            {
                UserId = targetUserId,
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _taskServicesMock.GetUserTasksAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var paged = result.Data!;
            await Assert.That(paged.TotalCount).IsEqualTo(1);
            await Assert.That(paged.Items[0].Id).IsEqualTo(validTask.Id);
            await Assert.That(paged.Items[0].Title).IsEqualTo("Zadanie target usera");
        }

        [Test]
        public async Task GetUserTasksAsync_MapsRelationsProperly()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var user = new ApplicationUser
            {
                Id = userId,
                UserName = $"User_{uniqueSuffix}",
                Email = $"user_{uniqueSuffix}@test.pl",
                FirstName = "Jan",
                LastName = "Kowalski"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Firma_{uniqueSuffix}",
                NIP = "1111111111",
                OwnerId = userId
            };

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Adam",
                LastName = "Klient",
                CompanyId = company.Id,
                OwnerId = userId,
                Owner = user,
                IsPrimary = true
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "PLN",
                Code = "PLN",
                DecimalPlaces = 2
            };

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Projekt Alfa",
                CompanyId = company.Id,
                CurrencyId = currency.Id,
                OwnerId = userId,
                Status = DealsStatusEnum.InProgress
            };

            var taskWithRelations = new Tasks
            {
                Id = Guid.NewGuid(),
                Title = "Zadanie powiązane",
                AssignedToId = userId,
                ContactId = contact.Id,
                DealId = deal.Id,
                DueAt = DateTime.UtcNow.AddDays(4),
                Status = TaskStatusEnum.InProgress,
                Priority = TaskPriorityEnum.High,
                Description = "Opis",
                IsDeleted = false
            };

            var taskWithoutRelations = new Tasks
            {
                Id = Guid.NewGuid(),
                Title = "Zadanie pojedyncze",
                AssignedToId = userId,
                ContactId = null,
                DealId = null,
                DueAt = DateTime.UtcNow.AddDays(5),
                Status = TaskStatusEnum.ToDo,
                Priority = TaskPriorityEnum.Low,
                Description = "Opis",
                IsDeleted = false
            };

            _contextMock.Users.Add(user);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.Add(contact);
            _contextMock.Currencies.Add(currency);
            _contextMock.Deals.Add(deal);
            _contextMock.Tasks.AddRange(taskWithRelations, taskWithoutRelations);
            await _contextMock.SaveChangesAsync();

            var command = new UserTaskListCommand
            {
                UserId = userId,
                PageNumber = 1,
                PageSize = 10,
                SortBy = "title",
                SortDescending = true
            };

            // Act
            var result = await _taskServicesMock.GetUserTasksAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var items = result.Data!.Items;
            var mappedWithRelations = items.First(t => t.Id == taskWithRelations.Id);
            await Assert.That(mappedWithRelations.ContactName).IsEqualTo("Adam Klient");
            await Assert.That(mappedWithRelations.DealName).IsEqualTo("Projekt Alfa");
            await Assert.That(mappedWithRelations.Status).IsEqualTo(TaskStatusEnum.InProgress.ToString());
            await Assert.That(mappedWithRelations.Priority).IsEqualTo(TaskPriorityEnum.High.ToString());

            var mappedWithoutRelations = items.First(t => t.Id == taskWithoutRelations.Id);
            await Assert.That(mappedWithoutRelations.ContactName).IsNull();
            await Assert.That(mappedWithoutRelations.DealName).IsNull();
        }

        [Test]
        public async Task GetUserTasksAsync_FiltersByStatusAndPriority()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var user = new ApplicationUser
            {
                Id = userId,
                UserName = $"FilterUser_{uniqueSuffix}",
                Email = $"filter_{uniqueSuffix}@t.pl",
                FirstName = "Filter",
                LastName = "User"
            };

            var taskMatching = new Tasks
            {
                Id = Guid.NewGuid(),
                Title = "Idealne",
                AssignedToId = userId,
                Status = TaskStatusEnum.InProgress,
                Priority = TaskPriorityEnum.High,
                DueAt = DateTime.UtcNow.AddDays(1),
                Description = "Opis",
                IsDeleted = false
            };

            var taskWrongStatus = new Tasks
            {
                Id = Guid.NewGuid(),
                Title = "Zły status",
                AssignedToId = userId,
                Status = TaskStatusEnum.ToDo,
                Priority = TaskPriorityEnum.High,
                DueAt = DateTime.UtcNow.AddDays(2),
                Description = "Opis",
                IsDeleted = false
            };

            var taskWrongPriority = new Tasks
            {
                Id = Guid.NewGuid(),
                Title = "Zły priorytet",
                AssignedToId = userId,
                Status = TaskStatusEnum.InProgress,
                Priority = TaskPriorityEnum.Low,
                DueAt = DateTime.UtcNow.AddDays(3),
                Description = "Opis",
                IsDeleted = false
            };

            _contextMock.Users.Add(user);
            _contextMock.Tasks.AddRange(taskMatching, taskWrongStatus, taskWrongPriority);
            await _contextMock.SaveChangesAsync();

            var command = new UserTaskListCommand
            {
                UserId = userId,
                Status = TaskStatusEnum.InProgress,
                Priority = TaskPriorityEnum.High,
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _taskServicesMock.GetUserTasksAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var items = result.Data!.Items;
            await Assert.That(items.Count).IsEqualTo(1);
            await Assert.That(items[0].Id).IsEqualTo(taskMatching.Id);
        }

        [Test]
        public async Task GetUserTasksAsync_WhenSearchTermProvided_SearchesByTitleDealAndContact()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var user = new ApplicationUser
            {
                Id = userId,
                UserName = $"Search_{uniqueSuffix}",
                Email = $"search_{uniqueSuffix}@test.pl",
                FirstName = "Search",
                LastName = "User"
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Firma_{uniqueSuffix}",
                NIP = "2222222222",
                OwnerId = userId
            };

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Stanisław",
                LastName = "Wyszukiwany",
                CompanyId = company.Id,
                OwnerId = userId,
                Owner = user,
                IsPrimary = true
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "EUR",
                Code = "EUR",
                DecimalPlaces = 2
            };

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Kontrakt Specjalny",
                CompanyId = company.Id,
                CurrencyId = currency.Id,
                OwnerId = userId,
                Status = DealsStatusEnum.ToDo
            };

            var taskByTitle = new Tasks
            {
                Id = Guid.NewGuid(),
                Title = "Raport kwartalny żółty",
                AssignedToId = userId,
                DueAt = DateTime.UtcNow.AddDays(1),
                Description = "Opis",
                IsDeleted = false
            };

            var taskByDeal = new Tasks
            {
                Id = Guid.NewGuid(),
                Title = "Analiza",
                AssignedToId = userId,
                DealId = deal.Id,
                DueAt = DateTime.UtcNow.AddDays(2),
                Description = "Opis",
                IsDeleted = false
            };

            var taskByContact = new Tasks
            {
                Id = Guid.NewGuid(),
                Title = "Spotkanie",
                AssignedToId = userId,
                ContactId = contact.Id,
                DueAt = DateTime.UtcNow.AddDays(3),
                Description = "Opis",
                IsDeleted = false
            };

            var taskIrrelevant = new Tasks
            {
                Id = Guid.NewGuid(),
                Title = "Niezwiązany temat",
                AssignedToId = userId,
                DueAt = DateTime.UtcNow.AddDays(4),
                Description = "Opis",
                IsDeleted = false
            };

            _contextMock.Users.Add(user);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.Add(contact);
            _contextMock.Currencies.Add(currency);
            _contextMock.Deals.Add(deal);
            _contextMock.Tasks.AddRange(taskByTitle, taskByDeal, taskByContact, taskIrrelevant);
            await _contextMock.SaveChangesAsync();

            var resultTitle = await _taskServicesMock.GetUserTasksAsync(new UserTaskListCommand
            {
                UserId = userId,
                SearchTerm = "zolty",
                PageNumber = 1,
                PageSize = 10
            });
            await Assert.That(resultTitle.Data!.Items.Count).IsEqualTo(1);
            await Assert.That(resultTitle.Data.Items[0].Id).IsEqualTo(taskByTitle.Id);

            var resultDeal = await _taskServicesMock.GetUserTasksAsync(new UserTaskListCommand
            {
                UserId = userId,
                SearchTerm = "Specjalny",
                PageNumber = 1,
                PageSize = 10
            });
            await Assert.That(resultDeal.Data!.Items.Count).IsEqualTo(1);
            await Assert.That(resultDeal.Data.Items[0].Id).IsEqualTo(taskByDeal.Id);

            var resultContact = await _taskServicesMock.GetUserTasksAsync(new UserTaskListCommand
            {
                UserId = userId,
                SearchTerm = "Stanislaw",
                PageNumber = 1,
                PageSize = 10
            });
            await Assert.That(resultContact.Data!.Items.Count).IsEqualTo(1);
            await Assert.That(resultContact.Data.Items[0].Id).IsEqualTo(taskByContact.Id);
        }

        [Test]
        public async Task GetUserTasksAsync_AppliesPaginationAndSortingCorrectly()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var user = new ApplicationUser
            {
                Id = userId,
                UserName = $"PageUser_{uniqueSuffix}",
                Email = $"page_{uniqueSuffix}@t.pl",
                FirstName = "Page",
                LastName = "User"
            };

            var baseDate = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
            var tasks = Enumerable.Range(1, 5).Select(i => new Tasks
            {
                Id = Guid.NewGuid(),
                Title = $"Task_{i}",
                AssignedToId = userId,
                DueAt = baseDate.AddDays(i),
                Status = TaskStatusEnum.ToDo,
                Priority = TaskPriorityEnum.Medium,
                Description = "Opis",
                IsDeleted = false
            }).ToList();

            _contextMock.Users.Add(user);
            _contextMock.Tasks.AddRange(tasks);
            await _contextMock.SaveChangesAsync();

            var command = new UserTaskListCommand
            {
                UserId = userId,
                PageNumber = 2,
                PageSize = 2,
                SortBy = "dueat",
                SortDescending = false
            };

            // Act
            var result = await _taskServicesMock.GetUserTasksAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var paged = result.Data!;
            await Assert.That(paged.TotalCount).IsEqualTo(5);
            await Assert.That(paged.TotalPages).IsEqualTo(3);
            await Assert.That(paged.PageNumber).IsEqualTo(2);
            await Assert.That(paged.Items.Count).IsEqualTo(2);
            await Assert.That(paged.HasPreviousPage).IsTrue();
            await Assert.That(paged.HasNextPage).IsTrue();
            await Assert.That(paged.Items[0].Title).IsEqualTo("Task_3");
            await Assert.That(paged.Items[1].Title).IsEqualTo("Task_4");
        }

        // ─── GetDealTasksAsync ──────────────────────────────────────────────────────

        [Test]
        public async Task GetDealTasksAsync_MapsRelationsAndAppliesPaginationCorrectly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();
            var assignedUserId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"Owner_{uniqueSuffix}",
                Email = $"owner_{uniqueSuffix}@test.pl",
                FirstName = "Właściciel",
                LastName = "Deala"
            };

            var assignedUser = new ApplicationUser
            {
                Id = assignedUserId,
                UserName = $"Assigned_{uniqueSuffix}",
                Email = $"assigned_{uniqueSuffix}@test.pl",
                FirstName = "Jan",
                LastName = "Pracownik"
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
                Name = $"Company_{uniqueSuffix}",
                NIP = "1234567890",
                OwnerId = ownerId,
                Owner = owner
            };

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Transakcja Testowa",
                CompanyId = company.Id,
                Company = company,
                CurrencyId = currency.Id,
                Currency = currency,
                OwnerId = ownerId,
                Owner = owner,
                CloseDate = DateTime.UtcNow
            };

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Adam",
                LastName = "Kowalski",
                CompanyId = company.Id,
                Company = company,
                OwnerId = ownerId,
                Owner = owner,
                IsPrimary = true
            };

            var task = new Tasks
            {
                Id = Guid.NewGuid(),
                Title = "Kontakt z klientem w sprawie blachy",
                Description = "Zadzwonić do firmy",
                DealId = deal.Id,
                Deal = deal,
                ContactId = contact.Id,
                Contact = contact,
                AssignedToId = assignedUserId,
                AssignedTo = assignedUser,
                DueAt = new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc),
                Status = TaskStatusEnum.InProgress,
                Priority = TaskPriorityEnum.High,
                IsDeleted = false
            };

            _contextMock.Users.AddRange(owner, assignedUser);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            _contextMock.Contacts.Add(contact);
            _contextMock.Deals.Add(deal);
            _contextMock.Tasks.Add(task);
            await _contextMock.SaveChangesAsync();

            var command = new SalesTaskListCommand
            {
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _taskServicesMock.GetDealTasksAsync(deal.Id, command, ownerId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var paged = result.Data!;
            await Assert.That(paged.TotalCount).IsEqualTo(1);

            var item = paged.Items.First();
            await Assert.That(item.Id).IsEqualTo(task.Id);
            await Assert.That(item.Title).IsEqualTo("Kontakt z klientem w sprawie blachy");
            await Assert.That(item.Status).IsEqualTo(TaskStatusEnum.InProgress.ToString());
            await Assert.That(item.Priority).IsEqualTo(TaskPriorityEnum.High.ToString());
            await Assert.That(item.AssignedToId).IsEqualTo(assignedUserId);
            await Assert.That(item.AssignedToFirstName).IsEqualTo("Jan");
            await Assert.That(item.AssignedToLastName).IsEqualTo("Pracownik");
            await Assert.That(item.ContactId).IsEqualTo(contact.Id);
            await Assert.That(item.ContactFirstName).IsEqualTo("Adam");
            await Assert.That(item.ContactLastName).IsEqualTo("Kowalski");
        }

        [Test]
        public async Task GetDealTasksAsync_ReturnsTasksOnlyForSpecifiedDeal()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"User_{uniqueSuffix}",
                Email = $"user_{uniqueSuffix}@test.pl",
                FirstName = "Tomasz",
                LastName = "Kowalski"
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "EUR",
                Code = "EUR",
                DecimalPlaces = 2
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Firma_{uniqueSuffix}",
                NIP = "9998887766",
                OwnerId = ownerId,
                Owner = owner
            };

            var targetDeal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Docelowy Deal",
                CompanyId = company.Id,
                Company = company,
                CurrencyId = currency.Id,
                Currency = currency,
                OwnerId = ownerId,
                Owner = owner,
                CloseDate = DateTime.UtcNow
            };

            var otherDeal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Inny Deal",
                CompanyId = company.Id,
                Company = company,
                CurrencyId = currency.Id,
                Currency = currency,
                OwnerId = ownerId,
                Owner = owner,
                CloseDate = DateTime.UtcNow
            };

            var targetTask = new Tasks
            {
                Id = Guid.NewGuid(),
                Title = "Zadanie z docelowego deala",
                DealId = targetDeal.Id,
                Deal = targetDeal,
                AssignedToId = ownerId,
                AssignedTo = owner,
                DueAt = DateTime.UtcNow.AddDays(1),
                Description = "Opis",
                IsDeleted = false
            };

            var otherTask = new Tasks
            {
                Id = Guid.NewGuid(),
                Title = "Zadanie z innego deala",
                DealId = otherDeal.Id,
                Deal = otherDeal,
                AssignedToId = ownerId,
                AssignedTo = owner,
                DueAt = DateTime.UtcNow.AddDays(2),
                Description = "Opis",
                IsDeleted = false
            };

            _contextMock.Users.Add(owner);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            _contextMock.Deals.AddRange(targetDeal, otherDeal);
            _contextMock.Tasks.AddRange(targetTask, otherTask);
            await _contextMock.SaveChangesAsync();

            var command = new SalesTaskListCommand { PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _taskServicesMock.GetDealTasksAsync(targetDeal.Id, command, ownerId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var items = result.Data!.Items;
            await Assert.That(items).Count().IsEqualTo(1);
            await Assert.That(items[0].Id).IsEqualTo(targetTask.Id);
        }

        [Test]
        public async Task GetDealTasksAsync_WhenDealDoesNotExist_Returns404NotFound()
        {
            // Arrange
            var randomDealId = Guid.NewGuid();
            var randomUserId = Guid.NewGuid();
            var command = new SalesTaskListCommand { PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _taskServicesMock.GetDealTasksAsync(randomDealId, command, randomUserId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.Message).IsEqualTo("Sale not found.");
        }

        [Test]
        public async Task GetDealTasksAsync_WhenUserIsNotOwnerNorManager_ThrowsForbiddenException()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();
            var unauthorizedUserId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"Owner_{uniqueSuffix}",
                Email = $"owner_{uniqueSuffix}@t.pl",
                FirstName = "Owner",
                LastName = "User"
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
                NIP = "1112223344",
                OwnerId = ownerId,
                Owner = owner
            };

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Prywatny Deal",
                CompanyId = company.Id,
                Company = company,
                CurrencyId = currency.Id,
                Currency = currency,
                OwnerId = ownerId,
                Owner = owner,
                CloseDate = DateTime.UtcNow
            };

            _contextMock.Users.Add(owner);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            _contextMock.Deals.Add(deal);
            await _contextMock.SaveChangesAsync();

            var command = new SalesTaskListCommand { PageNumber = 1, PageSize = 10 };

            // Act & Assert
            await Assert.That(async () => await _taskServicesMock.GetDealTasksAsync(deal.Id, command, unauthorizedUserId))
                .Throws<ForbiddenException>();
        }

        [Test]
        public async Task GetDealTasksAsync_WhenDealHasNoTasks_ReturnsEmptyListWithSuccessStatus()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var ownerId = Guid.NewGuid();

            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"EmptyOwner_{uniqueSuffix}",
                Email = $"empty_{uniqueSuffix}@t.pl",
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
                Name = $"EmptyComp_{uniqueSuffix}",
                NIP = "5556667788",
                OwnerId = ownerId,
                Owner = owner
            };

            var dealWithoutTasks = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Deal Bez Zadań",
                CompanyId = company.Id,
                Company = company,
                CurrencyId = currency.Id,
                Currency = currency,
                OwnerId = ownerId,
                Owner = owner,
                CloseDate = DateTime.UtcNow
            };

            _contextMock.Users.Add(owner);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            _contextMock.Deals.Add(dealWithoutTasks);
            await _contextMock.SaveChangesAsync();

            var command = new SalesTaskListCommand { PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _taskServicesMock.GetDealTasksAsync(dealWithoutTasks.Id, command, ownerId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.Items).IsEmpty();
        }
    }
}
