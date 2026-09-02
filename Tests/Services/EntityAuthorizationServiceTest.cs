using Domain.Models;
using Infrastructure;
using Infrastructure.Interceptors;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Services.Services;
using Testcontainers.PostgreSql;

namespace Tests.Services
{
    public class EntityAuthorizationServiceTest
    {
        private AppDbContext _contextMock = null!;
        private EntityAuthorizationService _entityAuthMock = null!;
        private static PostgreSqlContainer _dbContainer = null!;
        private static string _connectionString = null!;
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

            _entityAuthMock = new EntityAuthorizationService(_contextMock);
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


        // ─── CanModifyAsync ─────────────────────────────────────────────────

        [Test]
        public async Task CanModifyAsync_WhenUserIsResourceOwner_ReturnsTrue()
        {
            // Arrange
            var ownerId = Guid.NewGuid();

            // Act 
            var result = await _entityAuthMock.CanModifyAsync(ownerId, ownerId);

            // Assert
            await Assert.That(result).IsTrue();
        }

        [Test]
        public async Task CanModifyAsync_WhenUserIsManager_ReturnsTrue()
        {
            // Arrange
            var managerId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();

            var manager = new ApplicationUser
            {
                Id = managerId,
                UserName = "ManagerUser",
                Email = "manager@test.pl",
                FirstName = "Adam",
                LastName = "Manager"
            };

            var managerRole = new IdentityRole<Guid>
            {
                Id = Guid.NewGuid(),
                Name = "Manager",
                NormalizedName = "MANAGER"
            };

            var userRole = new IdentityUserRole<Guid>
            {
                UserId = managerId,
                RoleId = managerRole.Id
            };

            _contextMock.Users.Add(manager);
            _contextMock.Roles.Add(managerRole);
            _contextMock.UserRoles.Add(userRole);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _entityAuthMock.CanModifyAsync(managerId, ownerId);

            // Assert
            await Assert.That(result).IsTrue();
        }

        [Test]
        public async Task CanModifyAsync_WhenUserHasDifferentRole_ReturnsFalse()
        {
            // Arrange
            var regularUserId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();

            var user = new ApplicationUser
            {
                Id = regularUserId,
                UserName = "RegularUser",
                Email = "regular@test.pl",
                FirstName = "Jan",
                LastName = "Kowalski"
            };

            var employeeRole = new IdentityRole<Guid>
            {
                Id = Guid.NewGuid(),
                Name = "Employee",
                NormalizedName = "EMPLOYEE"
            };

            var userRole = new IdentityUserRole<Guid>
            {
                UserId = regularUserId,
                RoleId = employeeRole.Id
            };

            _contextMock.Users.Add(user);
            _contextMock.Roles.Add(employeeRole);
            _contextMock.UserRoles.Add(userRole);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _entityAuthMock.CanModifyAsync(regularUserId, ownerId);

            // Assert
            await Assert.That(result).IsFalse();
        }

        [Test]
        public async Task CanModifyAsync_WhenUserHasNoRolesAndIsNotOwner_ReturnsFalse()
        {
            // Arrange
            var regularUserId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();

            var user = new ApplicationUser
            {
                Id = regularUserId,
                UserName = "NoRoleUser",
                Email = "norole@test.pl",
                FirstName = "Piotr",
                LastName = "Nowak"
            };

            _contextMock.Users.Add(user);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _entityAuthMock.CanModifyAsync(regularUserId, ownerId);

            // Assert
            await Assert.That(result).IsFalse();
        }
    }
}
