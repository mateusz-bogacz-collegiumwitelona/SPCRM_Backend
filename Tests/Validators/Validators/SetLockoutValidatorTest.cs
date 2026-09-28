using Api.Request.User;
using Api.Validators.Validators.User;
using Domain.Constants;
using FluentValidation.TestHelper;

namespace Tests.Validators
{
    public class SetLockoutValidatorTest
    {
        private readonly SetLockoutValidator _validator = new();

        // ─── UserId ──────────────────────────────────────────────────────────

        [Test]
        public async Task Validate_WhenUserIdIsValid_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new SetLockoutRequest
            {
                UserId = Guid.NewGuid(),
                LockoutEnd = DateTime.UtcNow.AddHours(1)
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.UserId);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenUserIdIsEmptyGuid_ShouldHaveValidationError()
        {
            // Arrange
            var request = new SetLockoutRequest
            {
                UserId = Guid.Empty,
                LockoutEnd = DateTime.UtcNow.AddHours(1)
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.UserId);
            await Task.CompletedTask;
        }

        // ─── LockoutEnd ──────────────────────────────────────────────────────

        [Test]
        public async Task Validate_WhenLockoutEndIsNull_ShouldNotHaveValidationError()
        {
            // Arrange 
            var request = new SetLockoutRequest
            {
                UserId = Guid.NewGuid(),
                LockoutEnd = null
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.LockoutEnd);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenLockoutEndIsInFuture_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new SetLockoutRequest
            {
                UserId = Guid.NewGuid(),
                LockoutEnd = DateTime.UtcNow.AddDays(1)
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.LockoutEnd);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenLockoutEndIsInPast_ShouldHaveInvalidDateErrorCode()
        {
            // Arrange
            var request = new SetLockoutRequest
            {
                UserId = Guid.NewGuid(),
                LockoutEnd = DateTime.UtcNow.AddHours(-1)
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.LockoutEnd)
                  .WithErrorCode(ErrorCodes.InvalidDate);
            await Task.CompletedTask;
        }
    }
}
