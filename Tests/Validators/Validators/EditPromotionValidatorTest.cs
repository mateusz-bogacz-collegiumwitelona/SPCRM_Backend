using Api.Request.Promotion;
using Api.Validators.Validators.Promotion;
using Domain.Constants;
using FluentValidation.TestHelper;

namespace Tests.Validators
{
    public class EditPromotionValidatorTest
    {
        private readonly EditPromotionValidator _validator = new();

        // ─── Id ──────────────────────────────────────────────────────────────

        [Test]
        public async Task Validate_WhenIdIsValid_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new EditPromotionRequest
            {
                Id = Guid.NewGuid()
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Id);
            await Task.CompletedTask;
        }

        // ─── DiscountPercentage & PromotionalPrice ───────────────────────────

        [Test]
        public async Task Validate_WhenBothDiscountAndPriceAreNull_ShouldNotHaveValidationErrorForExclusiveRule()
        {
            // Arrange
            var request = new EditPromotionRequest
            {
                Id = Guid.NewGuid(),
                DiscountPercentage = null,
                PromotionalPrice = null
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenOnlyDiscountIsSet_ShouldNotHaveValidationErrorForExclusiveRule()
        {
            // Arrange
            var request = new EditPromotionRequest
            {
                Id = Guid.NewGuid(),
                DiscountPercentage = 10m,
                PromotionalPrice = null
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenOnlyPriceIsSet_ShouldNotHaveValidationErrorForExclusiveRule()
        {
            // Arrange
            var request = new EditPromotionRequest
            {
                Id = Guid.NewGuid(),
                DiscountPercentage = null,
                PromotionalPrice = 9999,
                CurrencyId = Guid.NewGuid()
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenBothDiscountAndPriceAreSet_ShouldHaveDiscountPercentageAndPriceCannotBothChoiceErrorCode()
        {
            // Arrange
            var request = new EditPromotionRequest
            {
                Id = Guid.NewGuid(),
                DiscountPercentage = 10m,
                PromotionalPrice = 9999,
                CurrencyId = Guid.NewGuid()
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x)
                  .WithErrorCode(ErrorCodes.DiscountPercentageAndPriceCannotBothChoice);
            await Task.CompletedTask;
        }

        // ─── CurrencyId ──────────────────────────────────────────────────────

        [Test]
        public async Task Validate_WhenPromotionalPriceHasValueAndCurrencyIdIsNull_ShouldHaveGuidRequiredErrorCode()
        {
            // Arrange
            var request = new EditPromotionRequest
            {
                Id = Guid.NewGuid(),
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
        public async Task Validate_WhenPromotionalPriceIsNull_ShouldNotHaveValidationErrorForCurrencyId()
        {
            // Arrange
            var request = new EditPromotionRequest
            {
                Id = Guid.NewGuid(),
                PromotionalPrice = null,
                CurrencyId = null
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.CurrencyId);
            await Task.CompletedTask;
        }

        // ─── Optional Fields (Name, ContactId, MinQuantity, MinWeight) ───────

        [Test]
        public async Task Validate_WhenOptionalFieldsAreNull_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new EditPromotionRequest
            {
                Id = Guid.NewGuid(),
                Name = null,
                ContactId = null,
                MinQuantity = null,
                MinWeight = null
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Name);
            result.ShouldNotHaveValidationErrorFor(x => x.ContactId);
            result.ShouldNotHaveValidationErrorFor(x => x.MinQuantity);
            result.ShouldNotHaveValidationErrorFor(x => x.MinWeight);
            await Task.CompletedTask;
        }
    }
}
