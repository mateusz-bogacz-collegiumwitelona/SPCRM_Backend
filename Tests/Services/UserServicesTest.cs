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
using Services.Services;
using Testcontainers.PostgreSql;

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

            _userServicesMock = new UserServices(
                _userManagerMock,
                _roleManagerMock,
                _contextMock,
                _loggerMock
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
    }
}
