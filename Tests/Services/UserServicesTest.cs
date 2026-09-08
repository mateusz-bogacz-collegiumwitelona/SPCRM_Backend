using Domain.Constants;
using Domain.Exceptions.Exception;
using Domain.Models;
using Infrastructure;
using Infrastructure.Interceptors;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Services.Command.User;
using Services.Services;
using Testcontainers.PostgreSql;
using Tests.Services.Fakes;

namespace Tests.Services
{
    public class UserServicesTest
    {
        protected AppDbContext _contextMock = null!;
        protected UserManager<ApplicationUser> _userManagerMock = null!;
        protected RoleManager<IdentityRole<Guid>> _roleManagerMock = null!;
        protected ILogger<UserServices> _loggerMock = null!;
        protected UserServices _userServicesMock = null!;
        private string _currentSchema = null!;
        private static PostgreSqlContainer _dbContainer = null!;
        private static string _connectionString = null!;
        protected FakeEmailSender _emailSenderMock = null!;


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

            var dataProtectionProvider = new EphemeralDataProtectionProvider();
            var tokenProviderOptions = Options.Create(new DataProtectionTokenProviderOptions());
            var tokenProviderLogger = NullLogger<DataProtectorTokenProvider<ApplicationUser>>.Instance;

            _userManagerMock.RegisterTokenProvider(
                TokenOptions.DefaultProvider,
                new DataProtectorTokenProvider<ApplicationUser>(dataProtectionProvider, tokenProviderOptions, tokenProviderLogger)
            );

            var roleStore = new RoleStore<IdentityRole<Guid>, AppDbContext, Guid>(_contextMock);
            _roleManagerMock = new RoleManager<IdentityRole<Guid>>(
                roleStore,
                null!,
                normalizer,
                null!,
                null!
            );

            _loggerMock = NullLogger<UserServices>.Instance;
            _emailSenderMock = new FakeEmailSender();

            _userServicesMock = new UserServices(
                _userManagerMock,
                _roleManagerMock,
                _contextMock,
                _loggerMock,
                _emailSenderMock
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

        // ─── GetUserSimpleListAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetUserSimpleListAsync_ReturnsOnlyConfirmedActiveNonAdminUsersSorted()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var userRole = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "User", NormalizedName = "USER" };
            var managerRole = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "Manager", NormalizedName = "MANAGER" };
            var adminRole = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "Admin", NormalizedName = "ADMIN" };

            var validUser1 = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"User1_{uniqueSuffix}",
                NormalizedUserName = $"USER1_{uniqueSuffix}",
                Email = $"u1_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"U1_{uniqueSuffix}@TEST.PL",
                FirstName = "Jan",
                LastName = "Kowalski",
                EmailConfirmed = true,
                LockoutEnd = null,
                IsDeleted = false
            };

            var validUser2 = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"User2_{uniqueSuffix}",
                NormalizedUserName = $"USER2_{uniqueSuffix}",
                Email = $"u2_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"U2_{uniqueSuffix}@TEST.PL",
                FirstName = "Adam",
                LastName = "Nowak",
                EmailConfirmed = true,
                LockoutEnd = null,
                IsDeleted = false
            };

            var adminUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"Admin_{uniqueSuffix}",
                NormalizedUserName = $"ADMIN_{uniqueSuffix}",
                Email = $"admin_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"ADMIN_{uniqueSuffix}@TEST.PL",
                FirstName = "Admin",
                LastName = "Systemowy",
                EmailConfirmed = true,
                LockoutEnd = null,
                IsDeleted = false
            };

            var unconfirmedUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"Unconfirmed_{uniqueSuffix}",
                NormalizedUserName = $"UNCONFIRMED_{uniqueSuffix}",
                Email = $"unconfirmed_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"UNCONFIRMED_{uniqueSuffix}@TEST.PL",
                FirstName = "Piotr",
                LastName = "Niepotwierdzony",
                EmailConfirmed = false,
                LockoutEnd = null,
                IsDeleted = false
            };

            var lockedUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"Locked_{uniqueSuffix}",
                NormalizedUserName = $"LOCKED_{uniqueSuffix}",
                Email = $"locked_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"LOCKED_{uniqueSuffix}@TEST.PL",
                FirstName = "Tomasz",
                LastName = "Zablokowany",
                EmailConfirmed = true,
                LockoutEnd = DateTimeOffset.UtcNow.AddDays(7),
                IsDeleted = false
            };

            var deletedUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"Deleted_{uniqueSuffix}",
                NormalizedUserName = $"DELETED_{uniqueSuffix}",
                Email = $"deleted_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"DELETED_{uniqueSuffix}@TEST.PL",
                FirstName = "Krzysztof",
                LastName = "Usuniety",
                EmailConfirmed = true,
                LockoutEnd = null,
                IsDeleted = true
            };

            _contextMock.Roles.AddRange(userRole, managerRole, adminRole);
            _contextMock.Users.AddRange(validUser1, validUser2, adminUser, unconfirmedUser, lockedUser, deletedUser);

            _contextMock.UserRoles.AddRange(
                new IdentityUserRole<Guid> { UserId = validUser1.Id, RoleId = userRole.Id },
                new IdentityUserRole<Guid> { UserId = validUser2.Id, RoleId = managerRole.Id },
                new IdentityUserRole<Guid> { UserId = adminUser.Id, RoleId = adminRole.Id },
                new IdentityUserRole<Guid> { UserId = unconfirmedUser.Id, RoleId = userRole.Id },
                new IdentityUserRole<Guid> { UserId = lockedUser.Id, RoleId = userRole.Id },
                new IdentityUserRole<Guid> { UserId = deletedUser.Id, RoleId = userRole.Id }
            );

            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _userServicesMock.GetUserSimpleListAsync();

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();

            var relevantUsers = result.Data!
                .Where(u => u.Id == validUser1.Id || u.Id == validUser2.Id || u.Id == adminUser.Id ||
                            u.Id == unconfirmedUser.Id || u.Id == lockedUser.Id || u.Id == deletedUser.Id)
                .ToList();

            await Assert.That(relevantUsers.Count).IsEqualTo(2);

            await Assert.That(relevantUsers[0].Id).IsEqualTo(validUser1.Id);
            await Assert.That(relevantUsers[0].LastName).IsEqualTo("Kowalski");

            await Assert.That(relevantUsers[1].Id).IsEqualTo(validUser2.Id);
            await Assert.That(relevantUsers[1].LastName).IsEqualTo("Nowak");
        }

        [Test]
        public async Task GetUserSimpleListAsync_WhenUserHasMultipleRoles_DoesNotReturnDuplicates()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var role1 = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "RoleA", NormalizedName = "ROLEA" };
            var role2 = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "RoleB", NormalizedName = "ROLEB" };

            var multiRoleUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"Multi_{uniqueSuffix}",
                NormalizedUserName = $"MULTI_{uniqueSuffix}",
                Email = $"multi_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"MULTI_{uniqueSuffix}@TEST.PL",
                FirstName = "Marek",
                LastName = "Wielorolowy",
                EmailConfirmed = true,
                LockoutEnd = null,
                IsDeleted = false
            };

            _contextMock.Roles.AddRange(role1, role2);
            _contextMock.Users.Add(multiRoleUser);
            _contextMock.UserRoles.AddRange(
                new IdentityUserRole<Guid> { UserId = multiRoleUser.Id, RoleId = role1.Id },
                new IdentityUserRole<Guid> { UserId = multiRoleUser.Id, RoleId = role2.Id }
            );

            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _userServicesMock.GetUserSimpleListAsync();

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();

            var occurrences = result.Data!.Count(u => u.Id == multiRoleUser.Id);
            await Assert.That(occurrences).IsEqualTo(1);
        }

        [Test]
        public async Task GetUserSimpleListAsync_WhenUserLockoutExpiredInPast_ReturnsUser()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userRole = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "UserRole", NormalizedName = "USERROLE" };

            var formerlyLockedUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"ExpiredLock_{uniqueSuffix}",
                NormalizedUserName = $"EXPIREDLOCK_{uniqueSuffix}",
                Email = $"expired_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"EXPIRED_{uniqueSuffix}@TEST.PL",
                FirstName = "Dawid",
                LastName = "Odblokowany",
                EmailConfirmed = true,
                LockoutEnd = DateTimeOffset.UtcNow.AddHours(-2),
                IsDeleted = false
            };

            _contextMock.Roles.Add(userRole);
            _contextMock.Users.Add(formerlyLockedUser);
            _contextMock.UserRoles.Add(new IdentityUserRole<Guid> { UserId = formerlyLockedUser.Id, RoleId = userRole.Id });

            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _userServicesMock.GetUserSimpleListAsync();

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            var found = result.Data!.Any(u => u.Id == formerlyLockedUser.Id);
            await Assert.That(found).IsTrue();
        }

        // ─── GetUserListAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetUserListAsync_MapsPropertiesAndRolesCorrectly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var managerRole = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "Manager", NormalizedName = "MANAGER" };
            var userRole = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "User", NormalizedName = "USER" };

            var activeUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"Active_{uniqueSuffix}",
                NormalizedUserName = $"ACTIVE_{uniqueSuffix}",
                Email = $"active_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"ACTIVE_{uniqueSuffix}@TEST.PL",
                FirstName = "Adam",
                LastName = "Kowalski",
                LockoutEnd = null,
                IsDeleted = false
            };

            var lockedUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"Locked_{uniqueSuffix}",
                NormalizedUserName = $"LOCKED_{uniqueSuffix}",
                Email = $"locked_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"LOCKED_{uniqueSuffix}@TEST.PL",
                FirstName = "Tomasz",
                LastName = "Nowak",
                LockoutEnd = DateTimeOffset.UtcNow.AddDays(2),
                IsDeleted = false
            };

            _contextMock.Roles.AddRange(managerRole, userRole);
            _contextMock.Users.AddRange(activeUser, lockedUser);
            _contextMock.UserRoles.AddRange(
                new IdentityUserRole<Guid> { UserId = activeUser.Id, RoleId = managerRole.Id },
                new IdentityUserRole<Guid> { UserId = lockedUser.Id, RoleId = userRole.Id }
            );
            await _contextMock.SaveChangesAsync();

            var command = new UserListCommand { PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _userServicesMock.GetUserListAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();

            var mappedActive = result.Data!.Items.FirstOrDefault(u => u.Id == activeUser.Id);
            await Assert.That(mappedActive).IsNotNull();
            await Assert.That(mappedActive!.FirstName).IsEqualTo("Adam");
            await Assert.That(mappedActive.LastName).IsEqualTo("Kowalski");
            await Assert.That(mappedActive.Role).IsEqualTo("Manager");
            await Assert.That(mappedActive.IsBlocked).IsFalse();

            var mappedLocked = result.Data!.Items.FirstOrDefault(u => u.Id == lockedUser.Id);
            await Assert.That(mappedLocked).IsNotNull();
            await Assert.That(mappedLocked!.IsBlocked).IsTrue();
            await Assert.That(mappedLocked.Role).IsEqualTo("User");
        }

        [Test]
        public async Task GetUserListAsync_ExcludesSoftDeletedUsers()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var role = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "User", NormalizedName = "USER" };

            var normalUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"Normal_{uniqueSuffix}",
                NormalizedUserName = $"NORMAL_{uniqueSuffix}",
                Email = $"normal_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"NORMAL_{uniqueSuffix}@T.PL",
                FirstName = "Normal",
                LastName = "User",
                IsDeleted = false
            };

            var deletedUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"Deleted_{uniqueSuffix}",
                NormalizedUserName = $"DELETED_{uniqueSuffix}",
                Email = $"del_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"DEL_{uniqueSuffix}@T.PL",
                FirstName = "Deleted",
                LastName = "User",
                IsDeleted = true
            };

            _contextMock.Roles.Add(role);
            _contextMock.Users.AddRange(normalUser, deletedUser);
            _contextMock.UserRoles.Add(new IdentityUserRole<Guid> { UserId = normalUser.Id, RoleId = role.Id });
            await _contextMock.SaveChangesAsync();

            var command = new UserListCommand { PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _userServicesMock.GetUserListAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            var items = result.Data!.Items;

            await Assert.That(items.Any(u => u.Id == normalUser.Id)).IsTrue();
            await Assert.That(items.Any(u => u.Id == deletedUser.Id)).IsFalse();
        }

        [Test]
        public async Task GetUserListAsync_WhenSearchTermProvided_SearchesByFirstNameLastNameAndRole()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var adminRole = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "Administrator", NormalizedName = "ADMINISTRATOR" };
            var guestRole = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "Guest", NormalizedName = "GUEST" };

            var user1 = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"U1_{uniqueSuffix}",
                NormalizedUserName = $"U1_{uniqueSuffix}",
                Email = $"u1_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"U1_{uniqueSuffix}@T.PL",
                FirstName = "Stanisław",
                LastName = "Borek"
            };

            var user2 = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"U2_{uniqueSuffix}",
                NormalizedUserName = $"U2_{uniqueSuffix}",
                Email = $"u2_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"U2_{uniqueSuffix}@T.PL",
                FirstName = "Michał",
                LastName = "Stanowski"
            };

            var user3 = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"U3_{uniqueSuffix}",
                NormalizedUserName = $"U3_{uniqueSuffix}",
                Email = $"u3_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"U3_{uniqueSuffix}@T.PL",
                FirstName = "Jerzy",
                LastName = "Zięba"
            };

            _contextMock.Roles.AddRange(adminRole, guestRole);
            _contextMock.Users.AddRange(user1, user2, user3);
            _contextMock.UserRoles.AddRange(
                new IdentityUserRole<Guid> { UserId = user1.Id, RoleId = guestRole.Id },
                new IdentityUserRole<Guid> { UserId = user2.Id, RoleId = guestRole.Id },
                new IdentityUserRole<Guid> { UserId = user3.Id, RoleId = adminRole.Id }
            );
            await _contextMock.SaveChangesAsync();

            var searchNameCommand = new UserListCommand { SearchTerm = "stan", PageNumber = 1, PageSize = 10 };
            var resultName = await _userServicesMock.GetUserListAsync(searchNameCommand);

            await Assert.That(resultName.IsSuccess).IsTrue();
            var nameItems = resultName.Data!.Items;
            await Assert.That(nameItems.Any(u => u.Id == user1.Id)).IsTrue();
            await Assert.That(nameItems.Any(u => u.Id == user2.Id)).IsTrue();
            await Assert.That(nameItems.Any(u => u.Id == user3.Id)).IsFalse();

            var searchRoleCommand = new UserListCommand { SearchTerm = "admin", PageNumber = 1, PageSize = 10 };
            var resultRole = await _userServicesMock.GetUserListAsync(searchRoleCommand);

            await Assert.That(resultRole.IsSuccess).IsTrue();
            var roleItems = resultRole.Data!.Items;
            await Assert.That(roleItems.Any(u => u.Id == user3.Id)).IsTrue();
            await Assert.That(roleItems.Any(u => u.Id == user1.Id)).IsFalse();
        }

        [Test]
        public async Task GetUserListAsync_AppliesPaginationCorrectly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var role = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "User", NormalizedName = "USER" };

            var users = Enumerable.Range(1, 5).Select(i => new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"PageUser{i}_{uniqueSuffix}",
                NormalizedUserName = $"PAGEUSER{i}_{uniqueSuffix}",
                Email = $"page{i}_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"PAGE{i}_{uniqueSuffix}@T.PL",
                FirstName = $"User{i}",
                LastName = uniqueSuffix
            }).ToList();

            _contextMock.Roles.Add(role);
            _contextMock.Users.AddRange(users);
            _contextMock.UserRoles.AddRange(users.Select(u => new IdentityUserRole<Guid> { UserId = u.Id, RoleId = role.Id }));
            await _contextMock.SaveChangesAsync();

            var command = new UserListCommand
            {
                SearchTerm = uniqueSuffix,
                PageNumber = 2,
                PageSize = 2
            };

            // Act
            var result = await _userServicesMock.GetUserListAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var paged = result.Data!;
            await Assert.That(paged.TotalCount).IsEqualTo(5);
            await Assert.That(paged.TotalPages).IsEqualTo(3);
            await Assert.That(paged.PageNumber).IsEqualTo(2);
            await Assert.That(paged.Items).Count().IsEqualTo(2);
            await Assert.That(paged.HasPreviousPage).IsTrue();
            await Assert.That(paged.HasNextPage).IsTrue();
        }

        // ─── GetAvailableOwnersAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetAvailableOwnersAsync_ExcludesAdminsAndMapsRolesCorrectly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var adminRole = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "Admin", NormalizedName = "ADMIN" };
            var managerRole = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "Manager", NormalizedName = "MANAGER" };
            var employeeRole = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "Employee", NormalizedName = "EMPLOYEE" };

            _contextMock.Roles.AddRange(adminRole, managerRole, employeeRole);

            var adminUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"Admin_{uniqueSuffix}",
                NormalizedUserName = $"ADMIN_{uniqueSuffix}",
                Email = $"admin_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"ADMIN_{uniqueSuffix}@T.PL",
                FirstName = "Adam",
                LastName = "Adminowski",
                IsDeleted = false
            };

            var managerUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"Manager_{uniqueSuffix}",
                NormalizedUserName = $"MANAGER_{uniqueSuffix}",
                Email = $"manager_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"MANAGER_{uniqueSuffix}@T.PL",
                FirstName = "Marek",
                LastName = "Menedżerski",
                IsDeleted = false
            };

            var employeeUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"Employee_{uniqueSuffix}",
                NormalizedUserName = $"EMPLOYEE_{uniqueSuffix}",
                Email = $"employee_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"EMPLOYEE_{uniqueSuffix}@T.PL",
                FirstName = "Piotr",
                LastName = "Pracowniczy",
                IsDeleted = false
            };

            _contextMock.Users.AddRange(adminUser, managerUser, employeeUser);

            _contextMock.UserRoles.AddRange(
                new IdentityUserRole<Guid> { UserId = adminUser.Id, RoleId = adminRole.Id },
                new IdentityUserRole<Guid> { UserId = managerUser.Id, RoleId = managerRole.Id },
                new IdentityUserRole<Guid> { UserId = employeeUser.Id, RoleId = employeeRole.Id }
            );

            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _userServicesMock.GetAvailableOwnersAsync();

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

            var mappedEmployee = data!.FirstOrDefault(u => u.Id == employeeUser.Id);
            await Assert.That(mappedEmployee).IsNotNull();
            await Assert.That(mappedEmployee!.Role).IsEqualTo("Employee");
        }

        [Test]
        public async Task GetAvailableOwnersAsync_WhenUserHasNoRole_ThrowsMissingUserRoleException()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var role = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "Manager", NormalizedName = "MANAGER" };
            _contextMock.Roles.Add(role);

            var validUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"Valid_{uniqueSuffix}",
                NormalizedUserName = $"VALID_{uniqueSuffix}",
                Email = $"valid_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"VALID_{uniqueSuffix}@T.PL",
                FirstName = "Marek",
                LastName = "Menedżerski",
                IsDeleted = false
            };

            var noRoleUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"NoRole_{uniqueSuffix}",
                NormalizedUserName = $"NOROLE_{uniqueSuffix}",
                Email = $"norole_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"NOROLE_{uniqueSuffix}@T.PL",
                FirstName = "Brak",
                LastName = "Roli",
                IsDeleted = false
            };

            _contextMock.Users.AddRange(validUser, noRoleUser);
            _contextMock.UserRoles.Add(new IdentityUserRole<Guid> { UserId = validUser.Id, RoleId = role.Id });
            await _contextMock.SaveChangesAsync();

            // Act & Assert
            await Assert.That(async () => await _userServicesMock.GetAvailableOwnersAsync())
                .Throws<MissingUserRoleException>();
        }

        // ─── CreateUserAsync ─────────────────────────────────────────────────

        [Test]
        public async Task CreateUserAsync_WhenValidData_CreatesUserAssignsRoleAndQueuesEmail()
        {
            // Arrange
            var roleName = "Salesman";
            await _roleManagerMock.CreateAsync(new IdentityRole<Guid> { Name = roleName, NormalizedName = roleName.ToUpperInvariant() });

            var command = new AddUserCommand
            {
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = "jan.kowalski@example.com",
                Role = roleName,
                Password = "Password123!"
            };

            // Act
            var result = await _userServicesMock.CreateUserAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status201Created);

            var createdUser = await _contextMock.Users.FirstOrDefaultAsync(u => u.Email == command.Email);
            await Assert.That(createdUser).IsNotNull();
            await Assert.That(createdUser!.FirstName).IsEqualTo("Jan");
            await Assert.That(createdUser.LastName).IsEqualTo("Kowalski");
            await Assert.That(createdUser.EmailConfirmed).IsFalse();
            await Assert.That(createdUser.NormalizedEmail).IsEqualTo(command.Email.ToUpperInvariant());

            var isUserInRole = await _userManagerMock.IsInRoleAsync(createdUser, roleName);
            await Assert.That(isUserInRole).IsTrue();

            await Assert.That(_emailSenderMock.SentCreateUserEmails).Count().IsEqualTo(1);
            var sentEmail = _emailSenderMock.SentCreateUserEmails[0];
            await Assert.That(sentEmail.Email).IsEqualTo(command.Email);
            await Assert.That(sentEmail.UserName).IsEqualTo(createdUser.UserName);
            await Assert.That(sentEmail.Token).IsNotNull();
            await Assert.That(sentEmail.Token).IsNotEmpty();
        }

        [Test]
        public async Task CreateUserAsync_WhenEmailAlreadyExists_ReturnsBadRequest()
        {
            // Arrange
            var existingEmail = "duplicate@example.com";
            var existingUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "jankow1",
                NormalizedUserName = "JANKOW1",
                Email = existingEmail,
                NormalizedEmail = existingEmail.ToUpperInvariant(),
                FirstName = "Jan",
                LastName = "Kowalski"
            };
            _contextMock.Users.Add(existingUser);
            await _contextMock.SaveChangesAsync();

            var command = new AddUserCommand
            {
                FirstName = "Adam",
                LastName = "Nowak",
                Email = existingEmail,
                Role = "Salesman",
                Password = "Password123!"
            };

            // Act
            var result = await _userServicesMock.CreateUserAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.UserAlreadyExists);
            await Assert.That(_emailSenderMock.SentCreateUserEmails).IsEmpty();
        }

        [Test]
        public async Task CreateUserAsync_WhenRoleAssignmentFails_RollsBackUserAndThrowsDataCorruptionException()
        {
            // Arrange
            var command = new AddUserCommand
            {
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = "missingrole@example.com",
                Role = "NonExistentRole",
                Password = "Password123!"
            };

            // Act & Assert
            await Assert.That(async () => await _userServicesMock.CreateUserAsync(command))
                .Throws<DataCorruptionException>();

            var userInDb = await _contextMock.Users.FirstOrDefaultAsync(u => u.Email == command.Email);
            await Assert.That(userInDb).IsNull();
            await Assert.That(_emailSenderMock.SentCreateUserEmails).IsEmpty();
        }

        // ─── ConfirmEmailAsync ───────────────────────────────────────────────────

        [Test]
        public async Task ConfirmEmailAsync_WhenValidTokenProvided_ConfirmsEmailSuccessfully()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var email = $"confirm_{uniqueSuffix}@test.pl";

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"User_{uniqueSuffix}",
                NormalizedUserName = $"USER_{uniqueSuffix}",
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                FirstName = "Jan",
                LastName = "Kowalski",
                EmailConfirmed = false
            };

            var createResult = await _userManagerMock.CreateAsync(user, "Password123!");
            await Assert.That(createResult.Succeeded).IsTrue();

            var validToken = await _userManagerMock.GenerateEmailConfirmationTokenAsync(user);

            var command = new ConfirmEmailCommand
            {
                Email = email,
                Token = validToken
            };

            // Act
            var result = await _userServicesMock.ConfirmEmailAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("Email confirmed successfully. You can now log in.");

            var updatedUser = await _userManagerMock.FindByEmailAsync(email);
            await Assert.That(updatedUser).IsNotNull();
            await Assert.That(updatedUser!.EmailConfirmed).IsTrue();
        }

        [Test]
        public async Task ConfirmEmailAsync_WhenUserDoesNotExist_Returns404NotFound()
        {
            // Arrange
            var command = new ConfirmEmailCommand
            {
                Email = "nonexistent@test.pl",
                Token = "dummy-token"
            };

            // Act
            var result = await _userServicesMock.ConfirmEmailAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.UserNotFound);
            await Assert.That(result.Message).IsEqualTo("User not found.");
        }

        [Test]
        public async Task ConfirmEmailAsync_WhenEmailAlreadyConfirmed_ReturnsBadRequest()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var email = $"already_confirmed_{uniqueSuffix}@test.pl";

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"Confirmed_{uniqueSuffix}",
                NormalizedUserName = $"CONFIRMED_{uniqueSuffix}",
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                FirstName = "Piotr",
                LastName = "Nowak",
                EmailConfirmed = true
            };

            var createResult = await _userManagerMock.CreateAsync(user, "Password123!");
            await Assert.That(createResult.Succeeded).IsTrue();

            var command = new ConfirmEmailCommand
            {
                Email = email,
                Token = "some-token"
            };

            // Act
            var result = await _userServicesMock.ConfirmEmailAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);
            await Assert.That(result.Message).IsEqualTo("Email is already confirmed.");
        }

        [Test]
        public async Task ConfirmEmailAsync_WhenTokenIsInvalidOrExpired_ReturnsBadRequest()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var email = $"invalid_token_{uniqueSuffix}@test.pl";

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"User_{uniqueSuffix}",
                NormalizedUserName = $"USER_{uniqueSuffix}",
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                FirstName = "Adam",
                LastName = "Kowalski",
                EmailConfirmed = false
            };

            var createResult = await _userManagerMock.CreateAsync(user, "Password123!");
            await Assert.That(createResult.Succeeded).IsTrue();

            var command = new ConfirmEmailCommand
            {
                Email = email,
                Token = "completely-invalid-or-tampered-token"
            };

            // Act
            var result = await _userServicesMock.ConfirmEmailAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.TokenInvalid);
            await Assert.That(result.Message).IsEqualTo("Invalid or expired confirmation token.");

            var userStillUnconfirmed = await _userManagerMock.FindByEmailAsync(email);
            await Assert.That(userStillUnconfirmed!.EmailConfirmed).IsFalse();
        }

        // ─── LockoutUserAsync ───────────────────────────────────────────────────

        [Test]
        public async Task LockoutUserAsync_WhenValidRequestWithSpecificDate_LocksUserAndSendsEmail()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userRole = "User";
            var adminId = Guid.NewGuid();

            await _roleManagerMock.CreateAsync(new IdentityRole<Guid> { Name = userRole, NormalizedName = userRole.ToUpperInvariant() });

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"User_{uniqueSuffix}",
                NormalizedUserName = $"USER_{uniqueSuffix}",
                Email = $"lock_target_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"LOCK_TARGET_{uniqueSuffix}@TEST.PL",
                FirstName = "Piotr",
                LastName = "Kowalski",
                EmailConfirmed = true,
                LockoutEnd = null
            };

            var createResult = await _userManagerMock.CreateAsync(user, "Password123!");
            await Assert.That(createResult.Succeeded).IsTrue();
            await _userManagerMock.AddToRoleAsync(user, userRole);

            var targetLockoutDate = DateTime.UtcNow.AddDays(14);
            var command = new SetLockoutCommand
            {
                UserId = user.Id,
                LockoutEnd = targetLockoutDate
            };

            // Act
            var result = await _userServicesMock.LockoutUserAsync(command, adminId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("User locked out successfully.");

            var updatedUser = await _userManagerMock.FindByIdAsync(user.Id.ToString());
            await Assert.That(updatedUser).IsNotNull();
            await Assert.That(updatedUser!.LockoutEnd.HasValue).IsTrue();
            await Assert.That(await _userManagerMock.IsLockedOutAsync(updatedUser)).IsTrue();

            await Assert.That(_emailSenderMock.SentLockoutEmails).Count().IsEqualTo(1);
            var sentMail = _emailSenderMock.SentLockoutEmails[0];
            await Assert.That(sentMail.Email).IsEqualTo(user.Email);
            var diff = Math.Abs((sentMail.LockoutEnd - targetLockoutDate).TotalSeconds);
            await Assert.That(diff < 2).IsTrue();
        }

        [Test]
        public async Task LockoutUserAsync_WhenLockoutEndIsNull_LocksUserPermanentlyWithMaxValue()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var adminId = Guid.NewGuid();

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"User_{uniqueSuffix}",
                NormalizedUserName = $"USER_{uniqueSuffix}",
                Email = $"perm_lock_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"PERM_LOCK_{uniqueSuffix}@TEST.PL",
                FirstName = "Tomasz",
                LastName = "Nowak",
                EmailConfirmed = true
            };

            var createResult = await _userManagerMock.CreateAsync(user, "Password123!");
            await Assert.That(createResult.Succeeded).IsTrue();

            var command = new SetLockoutCommand
            {
                UserId = user.Id,
                LockoutEnd = null
            };

            // Act
            var result = await _userServicesMock.LockoutUserAsync(command, adminId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var updatedUser = await _userManagerMock.FindByIdAsync(user.Id.ToString());
            await Assert.That(updatedUser).IsNotNull();
            await Assert.That(updatedUser!.LockoutEnd).IsEqualTo(DateTimeOffset.MaxValue);
            await Assert.That(await _userManagerMock.IsLockedOutAsync(updatedUser)).IsTrue();

            await Assert.That(_emailSenderMock.SentLockoutEmails).Count().IsEqualTo(1);
            await Assert.That(_emailSenderMock.SentLockoutEmails[0].LockoutEnd).IsEqualTo(DateTimeOffset.MaxValue);
        }

        [Test]
        public async Task LockoutUserAsync_WhenUserDoesNotExist_Returns404NotFound()
        {
            // Arrange
            var command = new SetLockoutCommand
            {
                UserId = Guid.NewGuid(),
                LockoutEnd = DateTime.UtcNow.AddDays(7)
            };

            // Act
            var result = await _userServicesMock.LockoutUserAsync(command, Guid.NewGuid());

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.UserNotFound);
            await Assert.That(result.Message).IsEqualTo("User not found.");
            await Assert.That(_emailSenderMock.SentLockoutEmails).IsEmpty();
        }

        [Test]
        public async Task LockoutUserAsync_WhenUserIsAdmin_Returns403ForbiddenAndDoesNotLockUser()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var adminRole = "Admin";
            var callingAdminId = Guid.NewGuid();

            await _roleManagerMock.CreateAsync(new IdentityRole<Guid> { Name = adminRole, NormalizedName = adminRole.ToUpperInvariant() });

            var targetAdminUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"Admin_{uniqueSuffix}",
                NormalizedUserName = $"ADMIN_{uniqueSuffix}",
                Email = $"target_admin_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"TARGET_ADMIN_{uniqueSuffix}@TEST.PL",
                FirstName = "Jan",
                LastName = "Administrator",
                EmailConfirmed = true,
                LockoutEnd = null
            };

            var createResult = await _userManagerMock.CreateAsync(targetAdminUser, "Password123!");
            await Assert.That(createResult.Succeeded).IsTrue();
            await _userManagerMock.AddToRoleAsync(targetAdminUser, adminRole);

            var command = new SetLockoutCommand
            {
                UserId = targetAdminUser.Id,
                LockoutEnd = DateTime.UtcNow.AddDays(30)
            };

            // Act
            var result = await _userServicesMock.LockoutUserAsync(command, callingAdminId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status403Forbidden);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.CannotBlockAdmin);
            await Assert.That(result.Message).IsEqualTo("Cannot ban an admin.");

            var refreshedAdmin = await _userManagerMock.FindByIdAsync(targetAdminUser.Id.ToString());
            await Assert.That(refreshedAdmin).IsNotNull();
            await Assert.That(refreshedAdmin!.LockoutEnd).IsNull();
            await Assert.That(_emailSenderMock.SentLockoutEmails).IsEmpty();
        }

        // ─── UnlockUserAsync ────────────────────────────────────────────────────

        [Test]
        public async Task UnlockUserAsync_WhenUserIsLockedOut_ClearsLockoutEndAndSendsEmail()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var email = $"locked_user_{uniqueSuffix}@test.pl";
            var adminId = Guid.NewGuid();

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"User_{uniqueSuffix}",
                NormalizedUserName = $"USER_{uniqueSuffix}",
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                FirstName = "Piotr",
                LastName = "Kowalski",
                EmailConfirmed = true,
                LockoutEnabled = true,
                LockoutEnd = DateTimeOffset.UtcNow.AddDays(7)
            };

            var createResult = await _userManagerMock.CreateAsync(user, "Password123!");
            await Assert.That(createResult.Succeeded).IsTrue();

            // Act
            var result = await _userServicesMock.UnlockUserAsync(user.Id, adminId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("User unlocked successfully.");

            var updatedUser = await _userManagerMock.FindByIdAsync(user.Id.ToString());
            await Assert.That(updatedUser).IsNotNull();
            await Assert.That(updatedUser!.LockoutEnd).IsNull();
            await Assert.That(await _userManagerMock.IsLockedOutAsync(updatedUser)).IsFalse();

            await Assert.That(_emailSenderMock.SentUnlockEmails).Count().IsEqualTo(1);
            await Assert.That(_emailSenderMock.SentUnlockEmails[0]).IsEqualTo(email);
        }

        [Test]
        public async Task UnlockUserAsync_WhenUserDoesNotExist_Returns404NotFound()
        {
            // Arrange
            var nonExistentUserId = Guid.NewGuid();
            var adminId = Guid.NewGuid();

            // Act
            var result = await _userServicesMock.UnlockUserAsync(nonExistentUserId, adminId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.UserNotFound);
            await Assert.That(result.Message).IsEqualTo("User not found.");
            await Assert.That(_emailSenderMock.SentUnlockEmails).IsEmpty();
        }

        [Test]
        public async Task UnlockUserAsync_WhenUserIsNotLockedOut_ReturnsBadRequest()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var email = $"active_user_{uniqueSuffix}@test.pl";
            var adminId = Guid.NewGuid();

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"ActiveUser_{uniqueSuffix}",
                NormalizedUserName = $"ACTIVEUSER_{uniqueSuffix}",
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                FirstName = "Adam",
                LastName = "Nowak",
                EmailConfirmed = true,
                LockoutEnd = null
            };

            var createResult = await _userManagerMock.CreateAsync(user, "Password123!");
            await Assert.That(createResult.Succeeded).IsTrue();

            // Act
            var result = await _userServicesMock.UnlockUserAsync(user.Id, adminId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.UserNotLockedOut);
            await Assert.That(result.Message).IsEqualTo("User is not locked out.");
            await Assert.That(_emailSenderMock.SentUnlockEmails).IsEmpty();
        }

        [Test]
        public async Task UnlockUserAsync_WhenLockoutExpiredInThePast_ReturnsBadRequest()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var email = $"expired_lock_{uniqueSuffix}@test.pl";
            var adminId = Guid.NewGuid();

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"PastLock_{uniqueSuffix}",
                NormalizedUserName = $"PASTLOCK_{uniqueSuffix}",
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                FirstName = "Marek",
                LastName = "Zeszly",
                EmailConfirmed = true,
                LockoutEnabled = true,
                LockoutEnd = DateTimeOffset.UtcNow.AddHours(-1)
            };

            var createResult = await _userManagerMock.CreateAsync(user, "Password123!");
            await Assert.That(createResult.Succeeded).IsTrue();

            // Act
            var result = await _userServicesMock.UnlockUserAsync(user.Id, adminId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.UserNotLockedOut);
            await Assert.That(result.Message).IsEqualTo("User is not locked out.");
            await Assert.That(_emailSenderMock.SentUnlockEmails).IsEmpty();
        }
    }
}

