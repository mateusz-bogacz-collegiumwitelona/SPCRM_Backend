using Domain.Constants;
using Domain.Models;
using DTO.Request;
using Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Services.Interfaces;
using Services.Services;
using Testcontainers.PostgreSql;
using TUnit.Core.Interfaces;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;

namespace Tests.Services
{
    public class AuthServicesTest
    {
        protected AppDbContext _contextMock = null!;
        protected UserManager<ApplicationUser> _userManagerMock = null!;
        protected RoleManager<IdentityRole<Guid>> _roleManagerMock = null!;
        protected ILogger<AuthServices> _loggerMock = null!;

        private static PostgreSqlContainer _dbContainer = null!;
        private static string _connectionString = null!;

        protected AuthServices _authServicesMock = null!;
        protected TokenServices _tokenServicesMock = null!;

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
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var configuration = new ConfigurationBuilder()
             .AddInMemoryCollection(new Dictionary<string, string?>
             {
                    {"JWT:KEY", "SuperTajnyKluczTestowyOodpowiedniejDlugosci123!"},
                    {"JWT:ISSUER", "TestIssuer"},
                    {"JWT:AUDIENCE", "TestAudience"},
                    {"FRONTEND:URL", "http://localhost:3000"}
             })
             .Build();

            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(_connectionString, options =>
                {
                    options.UseNetTopologySuite(); 
                })
                .Options;

            _contextMock = new AppDbContext(dbOptions);
            await _contextMock.Database.EnsureCreatedAsync();

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

            _loggerMock = Mock.Of<ILogger<AuthServices>>().Object;
            _tokenServicesMock = Mock.Of<TokenServices>(configuration).Object;

            _authServicesMock = new AuthServices(_userManagerMock, _tokenServicesMock, _loggerMock);
        }

        [After(Test)]
        public async Task CleanupAsync()
        {
            if (_contextMock != null)
            {
                await _contextMock.DisposeAsync();
            }
        }

        [Test]
        public async Task LoginAsync_WhenEmailIsNotConfirmed_Returns403()
        {
            // Arrange
            var user = new ApplicationUser
            {
                UserName = "Test1",
                NormalizedUserName = "TEST1",
                FirstName = "Test1",
                LastName = "Test1",
                Email = "test1@test.pl",
                NormalizedEmail = "TEST1@TEST1.PL",
                EmailConfirmed = false
            };

            await _userManagerMock.CreateAsync(user, "Password123!");

            var request = new LoginRequest { Name = "test1@test.pl", Password = "Password123!" };

            // Act
            var result = await _authServicesMock.LoginAsync(request);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status403Forbidden);
            await Assert.That(result.Message).IsEqualTo("Email is not confirmed.");
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.EmailNotConfirmed);
        }


        [Test]
        public async Task LoginAsync_WhenPasswordIsInvalid_Returns401()
        {
            // Arrange
            var passwordHasher = new PasswordHasher<ApplicationUser>();
            var user = new ApplicationUser
            {
                UserName = "Test2",
                NormalizedUserName = "TEST2",
                FirstName = "Test2",
                LastName = "Test2",
                Email = "test2@test.pl",
                NormalizedEmail = "TEST2@TEST2.PL",
                EmailConfirmed = true
            };

            await _userManagerMock.CreateAsync(user, "GoodPassword123!");

            var request = new LoginRequest { Name = "test2@test.pl", Password = "BadPassword123!" };

            // Act
            var result = await _authServicesMock.LoginAsync(request);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status401Unauthorized);
            await Assert.That(result.Message).IsEqualTo("Invalid username or password.");
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidCredentials);
        }

        [Test]
        public async Task LoginAsync_WhenUserHasNoRoles_Returns403()
        {
            // Arrange
            var user = new ApplicationUser
            {
                UserName = "Test3",
                NormalizedUserName = "TEST3",
                FirstName = "Test3",
                LastName = "Test3",
                Email = "test3@test.pl",
                NormalizedEmail = "TEST3@TEST3.PL",
                EmailConfirmed = true
            };

            await _userManagerMock.CreateAsync(user, "GoodPassword123!");

            var request = new LoginRequest { Name = "test3@test.pl", Password = "GoodPassword123!" };

            // Act
            var result = await _authServicesMock.LoginAsync(request);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status403Forbidden);
            await Assert.That(result.Message).IsEqualTo("User has no roles assigned.");
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.NoRolesAssigned);
        }

        [Test]
        public async Task LoginAsync_WhenCredentialsAreValid_Returns200()
        {
            // Arrange
            var user = new ApplicationUser
            {
                UserName = "Test4",
                NormalizedUserName = "TEST4",
                FirstName = "Test4",
                LastName = "Test4",
                Email = "test4@test.pl",
                NormalizedEmail = "TEST4@TEST4.PL",
                EmailConfirmed = true
            };

            await _userManagerMock.CreateAsync(user, "GoodPassword123!");

            await _roleManagerMock.CreateAsync(new IdentityRole<Guid>
            {
                Name = "User",
                NormalizedName = "USER"
            });

            await _userManagerMock.AddToRoleAsync(user, "User");

            var request = new LoginRequest { Name = "test4@test.pl", Password = "GoodPassword123!" };

            // Act
            var result = await _authServicesMock.LoginAsync(request);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("Login successful.");
        }
    }
}
