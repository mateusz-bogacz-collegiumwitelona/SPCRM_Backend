using Api.Request.Promotion;
using Api.Validators.Validators.Promotion;
using Domain.Constants;
using FluentValidation.TestHelper;

namespace Tests.Validators
{
    public class AddPromotionValidatorTest
    {
        private readonly AddPromotionValidator _validator = new();

        // ─── ProductId ───────────────────────────────────────────────────────

        [Test]
        public async Task Validate_WhenProductIdIsValid_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new AddPromotionRequest
            {
                Name = "Valid Promotion Name",
                ProductId = Guid.NewGuid()
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.ProductId);
            await Task.CompletedTask;
        }

        // ─── CurrencyId ──────────────────────────────────────────────────────

        [Test]
        public async Task Validate_WhenPromotionalPriceHasValueAndCurrencyIdIsNull_ShouldHaveGuidRequiredErrorCode()
        {
            // Arrange
            var request = new AddPromotionRequest
            {
                Name = "Valid Promotion Name",
                ProductId = Guid.NewGuid(),
                PromotionalPrice = 9999,
                CurrencyId = null
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.CurrencyId)
                  .WithErrorCode(ErrorCodes.GuidRequired);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenPromotionalPriceHasValueAndCurrencyIdIsValid_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new AddPromotionRequest
            {
                Name = "Valid Promotion Name",
                ProductId = Guid.NewGuid(),
                PromotionalPrice = 9999,
                CurrencyId = Guid.NewGuid()
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.CurrencyId);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenPromotionalPriceIsNull_ShouldNotHaveValidationErrorForCurrencyId()
        {
            // Arrange
            var request = new AddPromotionRequest
            {
                Name = "Valid Promotion Name",
                ProductId = Guid.NewGuid(),
                PromotionalPrice = null,
                CurrencyId = null
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.CurrencyId);
            await Task.CompletedTask;
        }

        // ─── Dates (StartDate & EndDate) ─────────────────────────────────────

        [Test]
        public async Task Validate_WhenEndDateIsAfterStartDate_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new AddPromotionRequest
            {
                Name = "Valid Promotion Name",
                ProductId = Guid.NewGuid(),
                DiscountPercentage = 10m,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(7)
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenEndDateIsBeforeStartDate_ShouldHaveInvalidDateErrorCode()
        {
            // Arrange
            var request = new AddPromotionRequest
            {
                Name = "Valid Promotion Name",
                ProductId = Guid.NewGuid(),
                StartDate = DateTime.UtcNow.AddDays(7),
                EndDate = DateTime.UtcNow
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x)
                  .WithErrorCode(ErrorCodes.InvalidDate);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenDatesAreNull_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new AddPromotionRequest
            {
                Name = "Valid Promotion Name",
                ProductId = Guid.NewGuid(),
                DiscountPercentage = 10m,
                StartDate = null,
                EndDate = null
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x);
            await Task.CompletedTask;
        }

        // ─── Optional Fields (ContactId, MinQuantity, MinWeight) ─────────────

        [Test]
        public async Task Validate_WhenOptionalFieldsAreNull_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new AddPromotionRequest
            {
                Name = "Valid Promotion Name",
                ProductId = Guid.NewGuid(),
                ContactId = null,
                MinQuantity = null,
                MinWeight = null
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.ContactId);
            result.ShouldNotHaveValidationErrorFor(x => x.MinQuantity);
            result.ShouldNotHaveValidationErrorFor(x => x.MinWeight);
            await Task.CompletedTask;
        }
    }
}
