using Domain.Constants;
using Domain.Enum;
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

            var passwordValidators = new List<IPasswordValidator<ApplicationUser>>
            {
                new PasswordValidator<ApplicationUser>()
            };

            var userValidators = new List<IUserValidator<ApplicationUser>>
            {
                new UserValidator<ApplicationUser>()
            };

            _userManagerMock = new UserManager<ApplicationUser>(
                userStore,
                identityOptions,
                passwordHasher,
                userValidators,
                passwordValidators,
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
        public async Task CreateUserAsync_WhenValidData_CreatesUserWithoutPasswordAssignsRoleAndQueuesEmail()
        {
            // Arrange
            var roleName = "Salesman";
            await _roleManagerMock.CreateAsync(new IdentityRole<Guid> { Name = roleName, NormalizedName = roleName.ToUpperInvariant() });

            var command = new AddUserCommand
            {
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = "jan.kowalski@example.com",
                Role = roleName
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
            await Assert.That(createdUser.PasswordHash).IsNull();

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
                Role = "Salesman"
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
                Role = "NonExistentRole"
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
        public async Task ConfirmEmailAsync_WhenValidTokenAndPasswordProvided_ConfirmsEmailAndSetsPassword()
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

            var createResult = await _userManagerMock.CreateAsync(user);
            await Assert.That(createResult.Succeeded).IsTrue();

            var validToken = await _userManagerMock.GenerateEmailConfirmationTokenAsync(user);

            var command = new ConfirmEmailCommand
            {
                Email = email,
                Token = validToken,
                Password = "Password123!"
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
            await Assert.That(await _userManagerMock.HasPasswordAsync(updatedUser)).IsTrue();
            await Assert.That(await _userManagerMock.CheckPasswordAsync(updatedUser, "Password123!")).IsTrue();
        }

        [Test]
        public async Task ConfirmEmailAsync_WhenUserDoesNotExist_Returns404NotFound()
        {
            // Arrange
            var command = new ConfirmEmailCommand
            {
                Email = "nonexistent@test.pl",
                Token = "dummy-token",
                Password = "Password123!"
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

            var createResult = await _userManagerMock.CreateAsync(user);
            await Assert.That(createResult.Succeeded).IsTrue();

            var command = new ConfirmEmailCommand
            {
                Email = email,
                Token = "some-token",
                Password = "Password123!"
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

            var createResult = await _userManagerMock.CreateAsync(user);
            await Assert.That(createResult.Succeeded).IsTrue();

            var command = new ConfirmEmailCommand
            {
                Email = email,
                Token = "completely-invalid-or-tampered-token",
                Password = "Password123!"
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
            await Assert.That(await _userManagerMock.HasPasswordAsync(userStillUnconfirmed)).IsFalse();
        }

        [Test]
        public async Task ConfirmEmailAsync_WhenPasswordDoesNotMeetRequirements_ReturnsBadRequestAndDoesNotLeaveUserConfirmed()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var email = $"weak_password_{uniqueSuffix}@test.pl";

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"User_{uniqueSuffix}",
                NormalizedUserName = $"USER_{uniqueSuffix}",
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                FirstName = "Krzysztof",
                LastName = "Krawczyk",
                EmailConfirmed = false
            };

            var createResult = await _userManagerMock.CreateAsync(user);
            await Assert.That(createResult.Succeeded).IsTrue();

            var validToken = await _userManagerMock.GenerateEmailConfirmationTokenAsync(user);

            var command = new ConfirmEmailCommand
            {
                Email = email,
                Token = validToken,
                Password = "123"
            };

            // Act
            var result = await _userServicesMock.ConfirmEmailAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.PasswordSetFailed);

            var refreshedUser = await _userManagerMock.FindByEmailAsync(email);
            await Assert.That(refreshedUser).IsNotNull();
            await Assert.That(refreshedUser!.EmailConfirmed).IsFalse();
            await Assert.That(await _userManagerMock.HasPasswordAsync(refreshedUser)).IsFalse();
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

        // ─── DeleteUserAsync ────────────────────────────────────────────────────

        [Test]
        public async Task DeleteUserAsync_WhenValidRequest_ReassignsActiveResourcesPreservesCompletedAndReleasesEmail()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var adminId = Guid.NewGuid();
            var userRole = "User";

            await _roleManagerMock.CreateAsync(new IdentityRole<Guid> { Name = userRole, NormalizedName = userRole.ToUpperInvariant() });

            var originalEmail = $"user_to_delete_{uniqueSuffix}@test.pl";
            var userToDelete = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"UserDelete_{uniqueSuffix}",
                NormalizedUserName = $"USERDELETE_{uniqueSuffix}",
                Email = originalEmail,
                NormalizedEmail = originalEmail.ToUpperInvariant(),
                FirstName = "Jan",
                LastName = "Kowalski",
                EmailConfirmed = true,
                IsDeleted = false
            };

            var targetUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"TargetUser_{uniqueSuffix}",
                NormalizedUserName = $"TARGETUSER_{uniqueSuffix}",
                Email = $"target_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"TARGET_{uniqueSuffix}@TEST.PL",
                FirstName = "Piotr",
                LastName = "Nowak",
                EmailConfirmed = true,
                IsDeleted = false
            };

            await _userManagerMock.CreateAsync(userToDelete, "Password123!");
            await _userManagerMock.CreateAsync(targetUser, "Password123!");
            await _userManagerMock.AddToRoleAsync(userToDelete, userRole);
            await _userManagerMock.AddToRoleAsync(targetUser, userRole);

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Firma {uniqueSuffix}",
                NIP = "1234567890",
                OwnerId = userToDelete.Id
            };

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Marek",
                LastName = "Klient",
                IsPrimary = true,
                CompanyId = company.Id,
                OwnerId = userToDelete.Id,
                Owner = userToDelete
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Code = $"C{uniqueSuffix[..2]}",
                Name = $"{uniqueSuffix[..2]}"
            };

            _contextMock.Currencies.Add(currency);

            var openDeal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Otarty Deal",
                Status = DealsStatusEnum.InProgress,
                CompanyId = company.Id,
                CurrencyId = currency.Id,
                OwnerId = userToDelete.Id
            };

            var completedDeal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Zrealizowany Deal",
                Status = DealsStatusEnum.Complete,
                CompanyId = company.Id,
                CurrencyId = currency.Id,
                OwnerId = userToDelete.Id
            };

            var openTask = new Tasks
            {
                Id = Guid.NewGuid(),
                Title = "Otwarte zadanie",
                Description = "Opis",
                Status = TaskStatusEnum.InProgress,
                Priority = TaskPriorityEnum.High,
                AssignedToId = userToDelete.Id
            };

            var completedTask = new Tasks
            {
                Id = Guid.NewGuid(),
                Title = "Wykonane zadanie",
                Description = "Opis",
                Status = TaskStatusEnum.Complete,
                Priority = TaskPriorityEnum.Low,
                AssignedToId = userToDelete.Id
            };

            _contextMock.Companies.Add(company);
            _contextMock.Contacts.Add(contact);
            _contextMock.Deals.AddRange(openDeal, completedDeal);
            _contextMock.Tasks.AddRange(openTask, completedTask);
            await _contextMock.SaveChangesAsync();

            var command = new DeleteUserCommand
            {
                UserId = userToDelete.Id,
                ReassignToUserId = targetUser.Id
            };

            // Act
            var result = await _userServicesMock.DeleteUserAsync(command, adminId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var updatedCompany = await _contextMock.Companies.FindAsync(company.Id);
            await Assert.That(updatedCompany!.OwnerId).IsEqualTo(targetUser.Id);

            var updatedContact = await _contextMock.Contacts.FindAsync(contact.Id);
            await Assert.That(updatedContact!.OwnerId).IsEqualTo(targetUser.Id);

            var updatedOpenDeal = await _contextMock.Deals.FindAsync(openDeal.Id);
            await Assert.That(updatedOpenDeal!.OwnerId).IsEqualTo(targetUser.Id);

            var updatedOpenTask = await _contextMock.Tasks.FindAsync(openTask.Id);
            await Assert.That(updatedOpenTask!.AssignedToId).IsEqualTo(targetUser.Id);

            var updatedCompletedDeal = await _contextMock.Deals.FindAsync(completedDeal.Id);
            await Assert.That(updatedCompletedDeal!.OwnerId).IsEqualTo(userToDelete.Id);

            var updatedCompletedTask = await _contextMock.Tasks.FindAsync(completedTask.Id);
            await Assert.That(updatedCompletedTask!.AssignedToId).IsEqualTo(userToDelete.Id);

            var deletedUserInDb = await _userManagerMock.FindByIdAsync(userToDelete.Id.ToString());
            await Assert.That(deletedUserInDb).IsNotNull();
            await Assert.That(deletedUserInDb!.IsDeleted).IsTrue();
            await Assert.That(deletedUserInDb.LockoutEnd).IsEqualTo(DateTimeOffset.MaxValue);
            await Assert.That(deletedUserInDb.Email!.StartsWith("deleted_")).IsTrue();
            await Assert.That(deletedUserInDb.UserName!.StartsWith("deleted_")).IsTrue();

            var reuseEmailUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"NewUser_{uniqueSuffix}",
                NormalizedUserName = $"NEWUSER_{uniqueSuffix}",
                Email = originalEmail,
                NormalizedEmail = originalEmail.ToUpperInvariant(),
                FirstName = "Nowy",
                LastName = "Uzytkownik"
            };

            var reCreateResult = await _userManagerMock.CreateAsync(reuseEmailUser, "Password123!");
            await Assert.That(reCreateResult.Succeeded).IsTrue();
        }

        [Test]
        public async Task DeleteUserAsync_WhenAdminAttemptsToDeleteThemselves_ReturnsBadRequest()
        {
            // Arrange
            var adminId = Guid.NewGuid();
            var command = new DeleteUserCommand
            {
                UserId = adminId,
                ReassignToUserId = Guid.NewGuid()
            };

            // Act
            var result = await _userServicesMock.DeleteUserAsync(command, adminId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);
            await Assert.That(result.Message).IsEqualTo("You cannot delete your own account.");
        }

        [Test]
        public async Task DeleteUserAsync_WhenUserToDeleteNotFound_Returns404NotFound()
        {
            // Arrange
            var command = new DeleteUserCommand
            {
                UserId = Guid.NewGuid(),
                ReassignToUserId = Guid.NewGuid()
            };

            // Act
            var result = await _userServicesMock.DeleteUserAsync(command, Guid.NewGuid());

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.UserNotFound);
            await Assert.That(result.Message).IsEqualTo("User not found.");
        }

        [Test]
        public async Task DeleteUserAsync_WhenTargetUserIsAdmin_Returns403Forbidden()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var adminRole = "Admin";
            await _roleManagerMock.CreateAsync(new IdentityRole<Guid> { Name = adminRole, NormalizedName = adminRole.ToUpperInvariant() });

            var adminToDelete = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"Admin_{uniqueSuffix}",
                NormalizedUserName = $"ADMIN_{uniqueSuffix}",
                Email = $"admin_del_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"ADMIN_DEL_{uniqueSuffix}@TEST.PL",
                FirstName = "Admin",
                LastName = "Jeden",
                IsDeleted = false
            };

            var reassignUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"User_{uniqueSuffix}",
                NormalizedUserName = $"USER_{uniqueSuffix}",
                Email = $"target_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"TARGET_{uniqueSuffix}@TEST.PL",
                FirstName = "Piotr",
                LastName = "Nowak",
                IsDeleted = false
            };

            await _userManagerMock.CreateAsync(adminToDelete, "Password123!");
            await _userManagerMock.CreateAsync(reassignUser, "Password123!");
            await _userManagerMock.AddToRoleAsync(adminToDelete, adminRole);

            var command = new DeleteUserCommand
            {
                UserId = adminToDelete.Id,
                ReassignToUserId = reassignUser.Id
            };

            // Act
            var result = await _userServicesMock.DeleteUserAsync(command, Guid.NewGuid());

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status403Forbidden);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.CannotBlockAdmin);
            await Assert.That(result.Message).IsEqualTo("Cannot delete an administrator account.");
        }

        [Test]
        public async Task DeleteUserAsync_WhenReassignUserDoesNotExistOrIsDeleted_ReturnsBadRequest()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");

            var userToDelete = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"User_{uniqueSuffix}",
                NormalizedUserName = $"USER_{uniqueSuffix}",
                Email = $"user_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"USER_{uniqueSuffix}@TEST.PL",
                FirstName = "Adam",
                LastName = "Kowalski",
                IsDeleted = false
            };

            var deletedTargetUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"DeletedTarget_{uniqueSuffix}",
                NormalizedUserName = $"DELETEDTARGET_{uniqueSuffix}",
                Email = $"deleted_target_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"DELETED_TARGET_{uniqueSuffix}@TEST.PL",
                FirstName = "Nieaktywny",
                LastName = "Target",
                IsDeleted = true
            };

            await _userManagerMock.CreateAsync(userToDelete, "Password123!");
            await _userManagerMock.CreateAsync(deletedTargetUser, "Password123!");

            var commandWithNonExistent = new DeleteUserCommand
            {
                UserId = userToDelete.Id,
                ReassignToUserId = Guid.NewGuid()
            };

            var commandWithDeletedTarget = new DeleteUserCommand
            {
                UserId = userToDelete.Id,
                ReassignToUserId = deletedTargetUser.Id
            };

            // Act
            var resultNonExistent = await _userServicesMock.DeleteUserAsync(commandWithNonExistent, Guid.NewGuid());
            var resultDeletedTarget = await _userServicesMock.DeleteUserAsync(commandWithDeletedTarget, Guid.NewGuid());

            // Assert
            await Assert.That(resultNonExistent.IsSuccess).IsFalse();
            await Assert.That(resultNonExistent.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(resultNonExistent.ErrorCode).IsEqualTo(ErrorCodes.UserNotFound);

            await Assert.That(resultDeletedTarget.IsSuccess).IsFalse();
            await Assert.That(resultDeletedTarget.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(resultDeletedTarget.ErrorCode).IsEqualTo(ErrorCodes.UserNotFound);
        }

        // ─── EditUserAsync ──────────────────────────────────────────────────────

        [Test]
        public async Task EditUserAsync_WhenValidFullUpdate_UpdatesUserFieldsAndNormalizesEmail()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var adminId = Guid.NewGuid();

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"User_{uniqueSuffix}",
                NormalizedUserName = $"USER_{uniqueSuffix}",
                Email = $"old_email_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"OLD_EMAIL_{uniqueSuffix}@TEST.PL",
                FirstName = "StareImie",
                LastName = "StareNazwisko",
                EmailConfirmed = true,
                IsDeleted = false
            };

            var createResult = await _userManagerMock.CreateAsync(user, "Password123!");
            await Assert.That(createResult.Succeeded).IsTrue();

            var command = new EditUserCommand
            {
                UserId = user.Id,
                FirstName = "NoweImie",
                LastName = "NoweNazwisko",
            };

            // Act
            var result = await _userServicesMock.EditUserAsync(command, adminId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("User updated successfully.");

            var updatedUser = await _userManagerMock.FindByIdAsync(user.Id.ToString());
            await Assert.That(updatedUser).IsNotNull();
            await Assert.That(updatedUser!.FirstName).IsEqualTo("NoweImie");
            await Assert.That(updatedUser.LastName).IsEqualTo("NoweNazwisko");
        }

        [Test]
        public async Task EditUserAsync_WhenPartialUpdate_OnlyModifiesProvidedFields()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var adminId = Guid.NewGuid();
            var originalEmail = $"original_{uniqueSuffix}@test.pl";

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"User_{uniqueSuffix}",
                NormalizedUserName = $"USER_{uniqueSuffix}",
                Email = originalEmail,
                NormalizedEmail = originalEmail.ToUpperInvariant(),
                FirstName = "OryginalneImie",
                LastName = "OryginalneNazwisko",
                EmailConfirmed = true,
                IsDeleted = false
            };

            var createResult = await _userManagerMock.CreateAsync(user, "Password123!");
            await Assert.That(createResult.Succeeded).IsTrue();

            var command = new EditUserCommand
            {
                UserId = user.Id,
                FirstName = "ZmienioneTylkoImie",
                LastName = null,
            };

            // Act
            var result = await _userServicesMock.EditUserAsync(command, adminId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var updatedUser = await _userManagerMock.FindByIdAsync(user.Id.ToString());
            await Assert.That(updatedUser).IsNotNull();
            await Assert.That(updatedUser!.FirstName).IsEqualTo("ZmienioneTylkoImie");
            await Assert.That(updatedUser.LastName).IsEqualTo("OryginalneNazwisko");
            await Assert.That(updatedUser.Email).IsEqualTo(originalEmail);
            await Assert.That(updatedUser.NormalizedEmail).IsEqualTo(originalEmail.ToUpperInvariant());
        }

        [Test]
        public async Task EditUserAsync_WhenUserNotFound_Returns404NotFound()
        {
            // Arrange
            var command = new EditUserCommand
            {
                UserId = Guid.NewGuid(),
                FirstName = "NoweImie",
                LastName = "NoweNazwisko",
            };

            // Act
            var result = await _userServicesMock.EditUserAsync(command, Guid.NewGuid());

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.UserNotFound);
            await Assert.That(result.Message).IsEqualTo("User not found.");
        }

        [Test]
        public async Task EditUserAsync_WhenUserIsSoftDeleted_Returns404NotFound()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var deletedUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"Deleted_{uniqueSuffix}",
                NormalizedUserName = $"DELETED_{uniqueSuffix}",
                Email = $"deleted_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"DELETED_{uniqueSuffix}@TEST.PL",
                FirstName = "Usuniety",
                LastName = "User",
                EmailConfirmed = true,
                IsDeleted = true
            };

            var createResult = await _userManagerMock.CreateAsync(deletedUser, "Password123!");
            await Assert.That(createResult.Succeeded).IsTrue();

            var command = new EditUserCommand
            {
                UserId = deletedUser.Id,
                FirstName = "ProbaZmiany"
            };

            // Act
            var result = await _userServicesMock.EditUserAsync(command, Guid.NewGuid());

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.UserNotFound);
            await Assert.That(result.Message).IsEqualTo("User not found.");
        }

        // ─── ChangeUserEmailAsync ──────────────────────────────────────────────

        [Test]
        public async Task ChangeUserEmailAsync_WhenValidRequest_SetsPendingEmailAndSendsEmails()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var adminId = Guid.NewGuid();
            var oldEmail = $"user_{uniqueSuffix}@test.pl";
            var newEmail = $"new_user_{uniqueSuffix}@test.pl";

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"User_{uniqueSuffix}",
                NormalizedUserName = $"USER_{uniqueSuffix}",
                Email = oldEmail,
                NormalizedEmail = oldEmail.ToUpperInvariant(),
                FirstName = "Piotr",
                LastName = "Nowak",
                EmailConfirmed = true,
                IsDeleted = false
            };

            await _userManagerMock.CreateAsync(user, "Password123!");

            var command = new ChangeUserEmailCommand
            {
                UserId = user.Id,
                NewEmail = newEmail
            };

            // Act
            var result = await _userServicesMock.ChangeUserEmailAsync(command, adminId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var refreshedUser = await _userManagerMock.FindByIdAsync(user.Id.ToString());
            await Assert.That(refreshedUser).IsNotNull();
            await Assert.That(refreshedUser!.Email).IsEqualTo(oldEmail);
            await Assert.That(refreshedUser.PendingEmail).IsEqualTo(newEmail.ToLowerInvariant());

            await Assert.That(_emailSenderMock.SentEmailChangeConfirmationLinks).Count().IsEqualTo(1);
            var sentConfirmation = _emailSenderMock.SentEmailChangeConfirmationLinks[0];
            await Assert.That(sentConfirmation.NewEmail).IsEqualTo(newEmail.ToLowerInvariant());
            await Assert.That(sentConfirmation.UserId).IsEqualTo(user.Id);
            await Assert.That(sentConfirmation.Token).IsNotNull();

            await Assert.That(_emailSenderMock.SentEmailChangeSecurityAlerts).Count().IsEqualTo(1);
            var sentAlert = _emailSenderMock.SentEmailChangeSecurityAlerts[0];
            await Assert.That(sentAlert.OldEmail).IsEqualTo(oldEmail);
            await Assert.That(sentAlert.NewEmail).IsEqualTo(newEmail.ToLowerInvariant());
        }

        [Test]
        public async Task ChangeUserEmailAsync_WhenNewEmailSameAsCurrent_ReturnsBadRequest()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var email = $"same_{uniqueSuffix}@test.pl";

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"User_{uniqueSuffix}",
                NormalizedUserName = $"USER_{uniqueSuffix}",
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                FirstName = "Piotr",
                LastName = "Nowak",
                EmailConfirmed = true,
                IsDeleted = false
            };

            await _userManagerMock.CreateAsync(user, "Password123!");

            var command = new ChangeUserEmailCommand
            {
                UserId = user.Id,
                NewEmail = email.ToUpperInvariant()
            };

            // Act
            var result = await _userServicesMock.ChangeUserEmailAsync(command, Guid.NewGuid());

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);
        }

        [Test]
        public async Task ChangeUserEmailAsync_WhenEmailOccupiedAsPendingByAnotherUser_ReturnsBadRequest()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var targetEmail = $"pending_target_{uniqueSuffix}@test.pl";

            var userA = new ApplicationUser
            {
                FirstName = $"UserA_{uniqueSuffix}",
                LastName = uniqueSuffix,
                Id = Guid.NewGuid(),
                UserName = $"UserA_{uniqueSuffix}",
                NormalizedUserName = $"USERA_{uniqueSuffix}",
                Email = $"userA_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"USERA_{uniqueSuffix}@TEST.PL",
                PendingEmail = targetEmail.ToLowerInvariant(),
                EmailConfirmed = true,
                IsDeleted = false
            };

            var userB = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                FirstName = $"UserB_{uniqueSuffix}",
                LastName = uniqueSuffix,
                UserName = $"UserB_{uniqueSuffix}",
                NormalizedUserName = $"USERB_{uniqueSuffix}",
                Email = $"userB_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"USERB_{uniqueSuffix}@TEST.PL",
                EmailConfirmed = true,
                IsDeleted = false
            };

            await _userManagerMock.CreateAsync(userA, "Password123!");
            await _userManagerMock.CreateAsync(userB, "Password123!");

            var command = new ChangeUserEmailCommand
            {
                UserId = userB.Id,
                NewEmail = targetEmail
            };

            // Act
            var result = await _userServicesMock.ChangeUserEmailAsync(command, Guid.NewGuid());

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.UserAlreadyExists);
        }

        // ─── ConfirmChangeUserEmailAsync ───────────────────────────────────────

        [Test]
        public async Task ConfirmChangeUserEmailAsync_WhenTokenValid_UpdatesEmailAndClearsPendingEmail()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var oldEmail = $"old_{uniqueSuffix}@test.pl";
            var newEmail = $"confirmed_{uniqueSuffix}@test.pl";

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"User_{uniqueSuffix}",
                NormalizedUserName = $"USER_{uniqueSuffix}",
                Email = oldEmail,
                NormalizedEmail = oldEmail.ToUpperInvariant(),
                PendingEmail = newEmail,
                FirstName = "Anna",
                LastName = "Kowalska",
                EmailConfirmed = true,
                IsDeleted = false
            };

            await _userManagerMock.CreateAsync(user, "Password123!");

            var validToken = await _userManagerMock.GenerateChangeEmailTokenAsync(user, newEmail);

            var command = new ConfirmChangeUserEmailCommand
            {
                UserId = user.Id,
                Token = validToken
            };

            // Act
            var result = await _userServicesMock.ConfirmChangeUserEmailAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var refreshedUser = await _userManagerMock.FindByIdAsync(user.Id.ToString());
            await Assert.That(refreshedUser).IsNotNull();
            await Assert.That(refreshedUser!.Email).IsEqualTo(newEmail);
            await Assert.That(refreshedUser.NormalizedEmail).IsEqualTo(newEmail.ToUpperInvariant());
            await Assert.That(refreshedUser.PendingEmail).IsNull();
            await Assert.That(refreshedUser.EmailConfirmed).IsTrue();
        }

        [Test]
        public async Task ConfirmChangeUserEmailAsync_WhenNoPendingEmail_ReturnsBadRequest()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                FirstName = "Anna",
                LastName = "Kowalska",
                UserName = $"User_{uniqueSuffix}",
                NormalizedUserName = $"USER_{uniqueSuffix}",
                Email = $"user_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"USER_{uniqueSuffix}@TEST.PL",
                PendingEmail = null,
                EmailConfirmed = true,
                IsDeleted = false
            };

            await _userManagerMock.CreateAsync(user, "Password123!");

            var command = new ConfirmChangeUserEmailCommand
            {
                UserId = user.Id,
                Token = "some-token"
            };

            // Act
            var result = await _userServicesMock.ConfirmChangeUserEmailAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);
            await Assert.That(result.Message).IsEqualTo("There is no pending email change request for this account.");
        }

        [Test]
        public async Task ConfirmChangeUserEmailAsync_WhenTokenInvalid_ReturnsBadRequest()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var newEmail = $"target_{uniqueSuffix}@test.pl";

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                FirstName = "Anna",
                LastName = "Kowalska",
                UserName = $"User_{uniqueSuffix}",
                NormalizedUserName = $"USER_{uniqueSuffix}",
                Email = $"current_{uniqueSuffix}@test.pl",
                NormalizedEmail = $"CURRENT_{uniqueSuffix}@TEST.PL",
                PendingEmail = newEmail,
                EmailConfirmed = true,
                IsDeleted = false
            };

            await _userManagerMock.CreateAsync(user, "Password123!");

            var command = new ConfirmChangeUserEmailCommand
            {
                UserId = user.Id,
                Token = "invalid-token-value"
            };

            // Act
            var result = await _userServicesMock.ConfirmChangeUserEmailAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.TokenInvalid);

            var refreshedUser = await _userManagerMock.FindByIdAsync(user.Id.ToString());
            await Assert.That(refreshedUser!.Email).IsEqualTo($"current_{uniqueSuffix}@test.pl");
            await Assert.That(refreshedUser.PendingEmail).IsEqualTo(newEmail);
        }
    }
}

