using Api.Request.Promotion;
using Api.Validators.Validators.Promotion;
using Domain.Constants;
using FluentValidation.TestHelper;

namespace Tests.Validators
{
    public class ActivatePromotionValidatorTest
    {
        private readonly ActivatePromotionValidator _validator = new();

        // ─── Id ──────────────────────────────────────────────────────────────

        [Test]
        public async Task Validate_WhenIdIsValid_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new ActivatePromotionRequest
            {
                Id = Guid.NewGuid(),
                EndDate = DateTime.UtcNow.AddDays(1)
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Id);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenIdIsEmptyGuid_ShouldHaveValidationError()
        {
            // Arrange
            var request = new ActivatePromotionRequest
            {
                Id = Guid.Empty,
                EndDate = DateTime.UtcNow.AddDays(1)
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Id);
            await Task.CompletedTask;
        }

        // ─── EndDate ─────────────────────────────────────────────────────────

        [Test]
        public async Task Validate_WhenEndDateIsInFuture_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new ActivatePromotionRequest
            {
                Id = Guid.NewGuid(),
                EndDate = DateTime.UtcNow.AddDays(1)
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.EndDate);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenEndDateIsEmpty_ShouldHaveInvalidDateErrorCode()
        {
            // Arrange
            var request = new ActivatePromotionRequest
            {
                Id = Guid.NewGuid(),
                EndDate = default
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.EndDate)
                  .WithErrorCode(ErrorCodes.InvalidDate);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenEndDateIsInPast_ShouldHaveInvalidDateErrorCode()
        {
            // Arrange
            var request = new ActivatePromotionRequest
            {
                Id = Guid.NewGuid(),
                EndDate = DateTime.UtcNow.AddDays(-1)
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.EndDate)
                  .WithErrorCode(ErrorCodes.InvalidDate);
            await Task.CompletedTask;
        }
    }
}
