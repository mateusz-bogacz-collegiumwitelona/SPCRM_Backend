using Api.Request.Promotion;
using Api.Validators.Validators.Promotion;
using Domain.Constants;
using FluentValidation.TestHelper;

namespace Tests.Validators
{
    public class PromotionListValidatorTest
    {
        private readonly PromotionListValidator _validator = new();

        // ─── DiscountPrecentage ──────────────────────────────────────────────

        [Test]
        public async Task Validate_WhenDiscountPrecentageFromIsNegative_ShouldHaveInvalidPromotionDiscountErrorCode()
        {
            // Arrange
            var request = new PromotionListRequest
            {
                DiscountPrecentageFrom = -1m
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.DiscountPrecentageFrom)
                  .WithErrorCode(ErrorCodes.InvalidPromotionDiscount);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenDiscountPrecentageToIsGreaterThan100_ShouldHaveInvalidPromotionDiscountErrorCode()
        {
            // Arrange
            var request = new PromotionListRequest
            {
                DiscountPrecentageTo = 101m
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.DiscountPrecentageTo)
                  .WithErrorCode(ErrorCodes.InvalidPromotionDiscount);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenDiscountPrecentageToIsLessThanFrom_ShouldHaveInvalidPromotionDiscountErrorCode()
        {
            // Arrange
            var request = new PromotionListRequest
            {
                DiscountPrecentageFrom = 50m,
                DiscountPrecentageTo = 40m
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.DiscountPrecentageTo)
                  .WithErrorCode(ErrorCodes.InvalidPromotionDiscount);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenDiscountPrecentagesAreValid_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new PromotionListRequest
            {
                DiscountPrecentageFrom = 10m,
                DiscountPrecentageTo = 20m
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.DiscountPrecentageFrom);
            result.ShouldNotHaveValidationErrorFor(x => x.DiscountPrecentageTo);
            await Task.CompletedTask;
        }

        // ─── PromotionPrice ──────────────────────────────────────────────────

        [Test]
        public async Task Validate_WhenPromotionPriceFromIsNegative_ShouldHaveInvalidPromotionPriceErrorCode()
        {
            // Arrange
            var request = new PromotionListRequest
            {
                PromotionPriceFrom = -1
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.PromotionPriceFrom)
                  .WithErrorCode(ErrorCodes.InvalidPromotionPrice);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenPromotionPriceToIsNegative_ShouldHaveInvalidPromotionPriceErrorCode()
        {
            // Arrange
            var request = new PromotionListRequest
            {
                PromotionPriceTo = -1
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.PromotionPriceTo)
                  .WithErrorCode(ErrorCodes.InvalidPromotionPrice);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenPromotionPriceToIsLessThanFrom_ShouldHaveInvalidPromotionPriceErrorCode()
        {
            // Arrange
            var request = new PromotionListRequest
            {
                PromotionPriceFrom = 5000,
                PromotionPriceTo = 4000
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.PromotionPriceTo)
                  .WithErrorCode(ErrorCodes.InvalidPromotionPrice);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenPromotionPricesAreValid_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new PromotionListRequest
            {
                PromotionPriceFrom = 1000,
                PromotionPriceTo = 2000
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.PromotionPriceFrom);
            result.ShouldNotHaveValidationErrorFor(x => x.PromotionPriceTo);
            await Task.CompletedTask;
        }

        // ─── Dates (FromDate & ToDate) ───────────────────────────────────────

        [Test]
        public async Task Validate_WhenToDateIsBeforeFromDate_ShouldHaveInvalidDateErrorCode()
        {
            // Arrange
            var request = new PromotionListRequest
            {
                FromDate = DateTime.UtcNow.AddDays(2),
                ToDate = DateTime.UtcNow
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.ToDate)
                  .WithErrorCode(ErrorCodes.InvalidDate);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenDatesAreValidAndToDateIsAfterFromDate_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new PromotionListRequest
            {
                FromDate = DateTime.UtcNow,
                ToDate = DateTime.UtcNow.AddDays(2)
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.ToDate);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenDatesAreNull_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new PromotionListRequest
            {
                FromDate = null,
                ToDate = null
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.ToDate);
            await Task.CompletedTask;
        }
    }
}
