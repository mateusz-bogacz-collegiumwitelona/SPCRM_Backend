using Api.Validators.Rule;
using Domain.Constants;
using FluentValidation;
using FluentValidation.TestHelper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Tests.Validators.Rules
{
    public class UserValidationRulesTest
    {
        private class TestUserModel
        {
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public string? RequiredEmail { get; set; }
            public string? OptionalEmail { get; set; }
            public string Password { get; set; } = string.Empty;
            public string ConfirmPassword { get; set; } = string.Empty;
            public string Role { get; set; } = string.Empty;
            public string Token { get; set; } = string.Empty;
        }

        private class TestUserModelValidator : AbstractValidator<TestUserModel>
        {
            public TestUserModelValidator(RoleManager<IdentityRole<Guid>> roleManager)
            {
                RuleFor(x => x.FirstName).ApplyFirstNameRules();
                RuleFor(x => x.LastName).ApplyLastNameRules();
                RuleFor(x => x.RequiredEmail).ApplyRequiredEmailRules();
                RuleFor(x => x.OptionalEmail).ApplyOptionalEmailRules();
                RuleFor(x => x.Password).ApplyPasswordRules();
                RuleFor(x => x.ConfirmPassword).ApplyConfirmPasswordRules(x => x.Password);
                RuleFor(x => x.Role).ApplyRoleRules(roleManager);
                RuleFor(x => x.Token).ApplyValidTokenRule();
            }
        }

        private readonly TestUserModelValidator _validator;

        public UserValidationRulesTest()
        {
            var fakeRoleManager = new FakeRoleManager();
            _validator = new TestUserModelValidator(fakeRoleManager);
        }

        // ─── ApplyFirstNameRules ─────────────────────────────────────────────

        [Test]
        [Arguments("Jan")]
        [Arguments("A")]
        public async Task ApplyFirstNameRules_WhenValidLength_ShouldNotHaveValidationError(string validName)
        {
            // Arrange
            var model = new TestUserModel { FirstName = validName };

            // Act
            var result = await _validator.TestValidateAsync(model, opt => opt.IncludeProperties(x => x.FirstName));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.FirstName);
        }

        [Test]
        [Arguments("")]
        [Arguments("   ")]
        [Arguments(null)]
        public async Task ApplyFirstNameRules_WhenEmptyOrNull_ShouldHaveInvalidFirstNameErrorCode(string? emptyName)
        {
            // Arrange
            var model = new TestUserModel { FirstName = emptyName };

            // Act
            var result = await _validator.TestValidateAsync(model, opt => opt.IncludeProperties(x => x.FirstName));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.FirstName)
                  .WithErrorCode(ErrorCodes.InvalidFirstName);
        }

        [Test]
        public async Task ApplyFirstNameRules_WhenExceeds50Characters_ShouldHaveInvalidFirstNameErrorCode()
        {
            // Arrange 
            var model = new TestUserModel { FirstName = new string('A', 51) };

            // Act
            var result = await _validator.TestValidateAsync(model, opt => opt.IncludeProperties(x => x.FirstName));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.FirstName)
                  .WithErrorCode(ErrorCodes.InvalidFirstName);
        }

        // ─── ApplyLastNameRules ──────────────────────────────────────────────

        [Test]
        [Arguments("Kowalski")]
        public async Task ApplyLastNameRules_WhenValidLength_ShouldNotHaveValidationError(string validLastName)
        {
            // Arrange
            var model = new TestUserModel { LastName = validLastName };

            // Act
            var result = await _validator.TestValidateAsync(model, opt => opt.IncludeProperties(x => x.LastName));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.LastName);
        }

        [Test]
        [Arguments("")]
        [Arguments("   ")]
        [Arguments(null)]
        public async Task ApplyLastNameRules_WhenEmptyOrNull_ShouldHaveInvalidLastNameErrorCode(string? emptyLastName)
        {
            // Arrange
            var model = new TestUserModel { LastName = emptyLastName };

            // Act
            var result = await _validator.TestValidateAsync(model, opt => opt.IncludeProperties(x => x.LastName));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.LastName)
                  .WithErrorCode(ErrorCodes.InvalidLastName);
        }

        [Test]
        public async Task ApplyLastNameRules_WhenExceeds50Characters_ShouldHaveInvalidLastNameErrorCode()
        {
            // Arrange 
            var model = new TestUserModel { LastName = new string('K', 51) };

            // Act
            var result = await _validator.TestValidateAsync(model, opt => opt.IncludeProperties(x => x.LastName));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.LastName)
                  .WithErrorCode(ErrorCodes.InvalidLastName);
        }

        // ─── ApplyRequiredEmailRules ─────────────────────────────────────────

        [Test]
        [Arguments("test@spcrm.pl")]
        [Arguments("jan.kowalski@firma.com")]
        public async Task ApplyRequiredEmailRules_WhenValidEmail_ShouldNotHaveValidationError(string validEmail)
        {
            // Arrange
            var model = new TestUserModel { RequiredEmail = validEmail };

            // Act
            var result = await _validator.TestValidateAsync(model, opt => opt.IncludeProperties(x => x.RequiredEmail));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.RequiredEmail);
        }

        [Test]
        [Arguments("")]
        [Arguments("   ")]
        [Arguments(null)]
        public async Task ApplyRequiredEmailRules_WhenEmptyOrNull_ShouldHaveEmailRequiredErrorCode(string? emptyEmail)
        {
            // Arrange
            var model = new TestUserModel { RequiredEmail = emptyEmail };

            // Act
            var result = await _validator.TestValidateAsync(model, opt => opt.IncludeProperties(x => x.RequiredEmail));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.RequiredEmail)
                  .WithErrorCode(ErrorCodes.EmailRequired);
        }

        [Test]
        [Arguments("brak_znaku_malpy")]
        [Arguments("@domena.pl")]
        [Arguments("uzytkownik@")]
        public async Task ApplyRequiredEmailRules_WhenInvalidFormat_ShouldHaveInvalidEmailErrorCode(string invalidEmail)
        {
            // Arrange
            var model = new TestUserModel { RequiredEmail = invalidEmail };

            // Act
            var result = await _validator.TestValidateAsync(model, opt => opt.IncludeProperties(x => x.RequiredEmail));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.RequiredEmail)
                  .WithErrorCode(ErrorCodes.InvalidEmail);
        }

        // ─── ApplyOptionalEmailRules ─────────────────────────────────────────

        [Test]
        [Arguments("opcjonalny@spcrm.pl")]
        [Arguments("")]
        [Arguments("   ")]
        [Arguments(null)]
        public async Task ApplyOptionalEmailRules_WhenValidOrEmpty_ShouldNotHaveValidationError(string? validOrEmptyEmail)
        {
            // Arrange
            var model = new TestUserModel { OptionalEmail = validOrEmptyEmail };

            // Act
            var result = await _validator.TestValidateAsync(model, opt => opt.IncludeProperties(x => x.OptionalEmail));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.OptionalEmail);
        }

        [Test]
        [Arguments("brak_malpy")]
        [Arguments("@brak_usera.pl")]
        public async Task ApplyOptionalEmailRules_WhenInvalidFormat_ShouldHaveInvalidEmailErrorCode(string invalidEmail)
        {
            // Arrange
            var model = new TestUserModel { OptionalEmail = invalidEmail };

            // Act
            var result = await _validator.TestValidateAsync(model, opt => opt.IncludeProperties(x => x.OptionalEmail));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.OptionalEmail)
                  .WithErrorCode(ErrorCodes.InvalidEmail);
        }

        // ─── ApplyPasswordRules ──────────────────────────────────────────────

        [Test]
        [Arguments("Password123!")]
        [Arguments("TrudneHaslo#2026")]
        public async Task ApplyPasswordRules_WhenValidPassword_ShouldNotHaveValidationError(string validPassword)
        {
            // Arrange
            var model = new TestUserModel { Password = validPassword };

            // Act
            var result = await _validator.TestValidateAsync(model, opt => opt.IncludeProperties(x => x.Password));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Password);
        }

        [Test]
        [Arguments("Pass1!")]
        [Arguments("TylkoLitery123")]
        public async Task ApplyPasswordRules_WhenInvalidFormat_ShouldHaveInvalidPasswordErrorCode(string invalidPassword)
        {
            // Arrange
            var model = new TestUserModel { Password = invalidPassword };

            // Act
            var result = await _validator.TestValidateAsync(model, opt => opt.IncludeProperties(x => x.Password));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Password)
                  .WithErrorCode(ErrorCodes.InvalidPassword);
        }

        // ─── ApplyConfirmPasswordRules ───────────────────────────────────────

        [Test]
        public async Task ApplyConfirmPasswordRules_WhenPasswordsMatch_ShouldNotHaveValidationError()
        {
            // Arrange
            var model = new TestUserModel
            {
                Password = "Password123!",
                ConfirmPassword = "Password123!"
            };

            // Act
            var result = await _validator.TestValidateAsync(model, opt => opt.IncludeProperties(x => x.ConfirmPassword));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.ConfirmPassword);
        }

        [Test]
        public async Task ApplyConfirmPasswordRules_WhenPasswordsDoNotMatch_ShouldHavePasswordMismatchErrorCode()
        {
            // Arrange
            var model = new TestUserModel
            {
                Password = "Password123!",
                ConfirmPassword = "InneHaslo123!"
            };

            // Act
            var result = await _validator.TestValidateAsync(model, opt => opt.IncludeProperties(x => x.ConfirmPassword));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.ConfirmPassword)
                  .WithErrorCode(ErrorCodes.PassowrdMismatch);
        }

        // ─── ApplyRoleRules ──────────────────────────────────────────────────

        [Test]
        [Arguments("Admin")]
        [Arguments("Manager")]
        public async Task ApplyRoleRules_WhenRoleExists_ShouldNotHaveValidationError(string existingRole)
        {
            // Arrange
            var model = new TestUserModel { Role = existingRole };

            // Act
            var result = await _validator.TestValidateAsync(model, opt => opt.IncludeProperties(x => x.Role));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Role);
        }

        [Test]
        [Arguments("NieznanaRola")]
        [Arguments("")]
        [Arguments("   ")]
        public async Task ApplyRoleRules_WhenRoleDoesNotExistOrEmpty_ShouldHaveInvalidRoleErrorCode(string nonExistingRole)
        {
            // Arrange
            var model = new TestUserModel { Role = nonExistingRole };

            // Act
            var result = await _validator.TestValidateAsync(model, opt => opt.IncludeProperties(x => x.Role));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Role)
                  .WithErrorCode(ErrorCodes.InvalidRole);
        }

        // ─── ApplyValidTokenRule ─────────────────────────────────────────────

        [Test]
        public async Task ApplyValidTokenRule_WhenTokenIsNotEmpty_ShouldNotHaveValidationError()
        {
            // Arrange
            var model = new TestUserModel { Token = "secure-token-12345" };

            // Act
            var result = await _validator.TestValidateAsync(model, opt => opt.IncludeProperties(x => x.Token));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Token);
        }

        [Test]
        [Arguments("")]
        [Arguments("   ")]
        public async Task ApplyValidTokenRule_WhenTokenIsEmptyOrWhitespace_ShouldHaveTokenInvalidErrorCode(string invalidToken)
        {
            // Arrange
            var model = new TestUserModel { Token = invalidToken };

            // Act
            var result = await _validator.TestValidateAsync(model, opt => opt.IncludeProperties(x => x.Token));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Token)
                  .WithErrorCode(ErrorCodes.TokenInvalid);
        }

        // ─── Fakes dla RoleManager ───────────────────────────────────────────

        private class FakeRoleManager : RoleManager<IdentityRole<Guid>>
        {
            public FakeRoleManager() : base(
                new FakeRoleStore(),
                Array.Empty<IRoleValidator<IdentityRole<Guid>>>(),
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                new LoggerFactory().CreateLogger<RoleManager<IdentityRole<Guid>>>())
            {
            }

            public override Task<bool> RoleExistsAsync(string roleName)
            {
                return Task.FromResult(roleName == "Admin" || roleName == "Manager");
            }
        }

        private class FakeRoleStore : IRoleStore<IdentityRole<Guid>>
        {
            public void Dispose() { }
            public Task<IdentityResult> CreateAsync(IdentityRole<Guid> role, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
            public Task<IdentityResult> UpdateAsync(IdentityRole<Guid> role, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
            public Task<IdentityResult> DeleteAsync(IdentityRole<Guid> role, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
            public Task<string> GetRoleIdAsync(IdentityRole<Guid> role, CancellationToken cancellationToken) => Task.FromResult(role.Id.ToString());
            public Task<string?> GetRoleNameAsync(IdentityRole<Guid> role, CancellationToken cancellationToken) => Task.FromResult(role.Name);
            public Task SetRoleNameAsync(IdentityRole<Guid> role, string? roleName, CancellationToken cancellationToken) => Task.CompletedTask;
            public Task<string?> GetNormalizedRoleNameAsync(IdentityRole<Guid> role, CancellationToken cancellationToken) => Task.FromResult(role.NormalizedName);
            public Task SetNormalizedRoleNameAsync(IdentityRole<Guid> role, string? normalizedName, CancellationToken cancellationToken) => Task.CompletedTask;
            public Task<IdentityRole<Guid>?> FindByIdAsync(string roleId, CancellationToken cancellationToken) => Task.FromResult<IdentityRole<Guid>?>(null);
            public Task<IdentityRole<Guid>?> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken) => Task.FromResult<IdentityRole<Guid>?>(null);
        }
    }
}
