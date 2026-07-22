using Domain.Constants;
using Domain.Models;
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
using Services.Command;
using Services.Services;
using Testcontainers.PostgreSql;

namespace Tests.Services
{
    public class AuthServicesTest
    {
        protected AppDbContext _contextMock = null!;
        protected UserManager<ApplicationUser> _userManagerMock = null!;
        protected RoleManager<IdentityRole<Guid>> _roleManagerMock = null!;
        protected ILogger<AuthServices> _loggerMock = null!;
        private string _currentSchema = null!;

        private static PostgreSqlContainer _dbContainer = null!;
        private static string _connectionString = null!;

        protected AuthServices _authServicesMock = null!;
        protected TokenServices _tokenServicesMock = null!;
        protected SignInManager<ApplicationUser> _signInManagerMock = null!;

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
                    options.MigrationsHistoryTable("__EFMigrationsHistory", _currentSchema);
                })
                .Options;

            _contextMock = new AppDbContext(dbOptions);
            await _contextMock.Database.ExecuteSqlRawAsync($"SET search_path TO {_currentSchema}");
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

            var fakeSignInManager = new FakeSignInManager(_userManagerMock);

            _signInManagerMock = fakeSignInManager;

            _authServicesMock = new AuthServices(_userManagerMock, _tokenServicesMock, _loggerMock, fakeSignInManager);
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

        [Test]
        public async Task LoginAsync_WhenEmailIsNotConfirmed_Returns401()
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

            var request = new LoginCommand { Name = "test1@test.pl", Password = "Password123!" };

            // Act
            var result = await _authServicesMock.LoginAsync(request);

            // Assert
            await Assert.That(result).IsEqualTo(StatusCodes.Status401Unauthorized);
        }


        [Test]
        public async Task LoginAsync_WhenPasswordIsInvalid_Returns401()
        {
            // Arrange
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

            var request = new LoginCommand { Name = "test2@test.pl", Password = "BadPassword123!" };

            // Act
            var result = await _authServicesMock.LoginAsync(request);

            // Assert
            await Assert.That(result).IsEqualTo(StatusCodes.Status401Unauthorized);
        }

        [Test]
        public async Task LoginAsync_WhenUserHasNoRoles_Returns401()
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

            var request = new LoginCommand { Name = "test3@test.pl", Password = "GoodPassword123!" };

            // Act
            var result = await _authServicesMock.LoginAsync(request);

            // Assert
            await Assert.That(result).IsEqualTo(StatusCodes.Status401Unauthorized);
        }

        [Test]
        public async Task LoginAsync_WhenCredentialsAreValid_Returns204()
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

            var request = new LoginCommand { Name = "test4@test.pl", Password = "GoodPassword123!" };

            // Act
            var result = await _authServicesMock.LoginAsync(request);

            // Assert
            await Assert.That(result).IsEqualTo(StatusCodes.Status204NoContent);
        }

        [Test]
        public async Task LogoutAsync_WhenCalled_Returns204AndSignsOut()
        {
            // Act
            var result = await _authServicesMock.LogoutAsync();

            // Assert
            await Assert.That(result).IsEqualTo(StatusCodes.Status204NoContent);

            var fakeSignInManager = (FakeSignInManager)_signInManagerMock;

            await Assert.That(fakeSignInManager.SignOutCalled).IsTrue();
        }

        [Test]
        public async Task GetUserDataAsync_WhenUserIsntLogin_Return404() 
        {
            // Act 
            var result = await _authServicesMock.GetUserDataAsync(Guid.NewGuid());

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.Message).IsEqualTo("User not found");
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.UserNotFound.ToString());
        }

        [Test]
        public async Task GetUserDataAsync_WhenUserDontHaveRoles_Return404()
        {
            // Arrange
            var user = new ApplicationUser
            {
                UserName = "Test5",
                NormalizedUserName = "TEST5",
                FirstName = "Test5",
                LastName = "Test5",
                Email = "test5@test.pl",
                NormalizedEmail = "TEST5@TEST5.PL",
                EmailConfirmed = true
            };

            await _userManagerMock.CreateAsync(user, "GoodPassword123!");

            // Act
            var result = await _authServicesMock.GetUserDataAsync(user.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.Message).IsEqualTo("User has no roles assigned");
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.NoRolesAssigned.ToString());
        }

        [Test]
        public async Task GetUserDataAsync_WhenEverythingIsFine_ReturnsUserData()
        {
            // Arrange
            var user = new ApplicationUser
            {
                UserName = "Test6",
                NormalizedUserName = "TEST6",
                FirstName = "Test6",
                LastName = "Test6",
                Email = "test6@test.pl",
                NormalizedEmail = "TEST6@TEST6.PL",
                EmailConfirmed = true
            };

            await _userManagerMock.CreateAsync(user, "GoodPassword123!");

            string roleName = "User" + Guid.NewGuid().ToString("N");

            await _roleManagerMock.CreateAsync(new IdentityRole<Guid>
            {
                Name = roleName,
                NormalizedName = roleName.ToUpper()
            });

            await _userManagerMock.AddToRoleAsync(user, "User");

            // Act
            var result = await _authServicesMock.GetUserDataAsync(user.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("User data retrieved successfully");
            await Assert.That(result.Data!.UserId).IsEqualTo(user.Id);
            await Assert.That(result.Data!.Email).IsEqualTo(user.Email);
            await Assert.That(result.Data!.UserName).IsEqualTo(user.UserName);
            await Assert.That(result.Data!.Roles).Contains("User");
        }
    }

    public class DummyClaimsFactory : IUserClaimsPrincipalFactory<ApplicationUser>
    {
        public Task<System.Security.Claims.ClaimsPrincipal> CreateAsync(ApplicationUser user)
            => Task.FromResult(new System.Security.Claims.ClaimsPrincipal());
    }

    public class FakeSignInManager : SignInManager<ApplicationUser>
    {
        public bool SignOutCalled { get; private set; } = false;

        public FakeSignInManager(UserManager<ApplicationUser> userManager)
            : base(userManager,
                   new HttpContextAccessor(),
                   new DummyClaimsFactory(),
                   Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
                   new NullLogger<SignInManager<ApplicationUser>>(),
                   null!,
                   null!)
        {
        }

        public override Task<Microsoft.AspNetCore.Identity.SignInResult> PasswordSignInAsync(
            string userName, string password, bool isPersistent, bool lockoutOnFailure)
            => Task.FromResult(Microsoft.AspNetCore.Identity.SignInResult.Success);

        public override Task SignOutAsync()
        {
            SignOutCalled = true;
            return Task.CompletedTask;
        }
    }
}
