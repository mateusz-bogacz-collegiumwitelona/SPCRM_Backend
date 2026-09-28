using Api.Request.Offer;
using Api.Validators.Validators.Offer;
using Domain.Constants;
using FluentValidation.TestHelper;

namespace Tests.Validators
{
    public class OfferProductItemValidatorTest
    {
        private readonly OfferProductItemValidator _validator = new();

        // ─── ProductId ───────────────────────────────────────────────────────

        [Test]
        public async Task Validate_WhenProductIdIsValid_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new OfferProductItemRequest
            {
                ProductId = Guid.NewGuid(),
                Quantity = 10,
                QuotedPrice = 10000
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.ProductId);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenProductIdIsEmpty_ShouldHaveValidationError()
        {
            // Arrange
            var request = new OfferProductItemRequest
            {
                ProductId = Guid.Empty,
                Quantity = 10,
                QuotedPrice = 10000
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.ProductId);
            await Task.CompletedTask;
        }

        // ─── Quantity ────────────────────────────────────────────────────────

        [Test]
        public async Task Validate_WhenQuantityIsGreaterThanZero_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new OfferProductItemRequest
            {
                ProductId = Guid.NewGuid(),
                Quantity = 1,
                QuotedPrice = 10000
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Quantity);
            await Task.CompletedTask;
        }

        [Arguments(0)]
        [Arguments(-1)]
        [Test]
        public async Task Validate_WhenQuantityIsZeroOrLess_ShouldHaveOfferQuantityInvalidErrorCode(int quantity)
        {
            // Arrange
            var request = new OfferProductItemRequest
            {
                ProductId = Guid.NewGuid(),
                Quantity = quantity,
                QuotedPrice = 10000
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Quantity)
                  .WithErrorCode(ErrorCodes.OfferQuantityInvalid);
            await Task.CompletedTask;
        }

        // ─── QuotedPrice ─────────────────────────────────────────────────────

        [Test]
        public async Task Validate_WhenQuotedPriceIsGreaterThanZero_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new OfferProductItemRequest
            {
                ProductId = Guid.NewGuid(),
                Quantity = 10,
                QuotedPrice = 10000
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.QuotedPrice);
            await Task.CompletedTask;
        }

        [Arguments(0L)]
        [Arguments(-10000L)]
        [Test]
        public async Task Validate_WhenQuotedPriceIsZeroOrLess_ShouldHaveOfferQuotedPriceInvalidErrorCode(long price)
        {
            // Arrange
            var request = new OfferProductItemRequest
            {
                ProductId = Guid.NewGuid(),
                Quantity = 10,
                QuotedPrice = price
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.QuotedPrice)
                  .WithErrorCode(ErrorCodes.OfferQuotedPriceInvalid);
            await Task.CompletedTask;
        }
    }
}
