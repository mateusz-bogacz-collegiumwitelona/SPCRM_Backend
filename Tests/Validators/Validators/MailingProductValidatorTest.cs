using Api.Request.Mailing;
using Api.Validators.Validators.Mailing;
using Domain.Constants;
using FluentValidation.TestHelper;

namespace Tests.Validators
{
    public class MailingProductValidatorTest
    {
        private readonly MailingProductValidator _validator = new();

        // ─── ProductId ───────────────────────────────────────────────────────

        [Test]
        public async Task Validate_WhenProductIdIsValid_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new MailingProductRequest
            {
                ProductId = Guid.NewGuid(),
                Quantity = 1
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
            var request = new MailingProductRequest
            {
                ProductId = Guid.Empty,
                Quantity = 1
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
            var request = new MailingProductRequest
            {
                ProductId = Guid.NewGuid(),
                Quantity = 1
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Quantity);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments(0)]
        [Arguments(-1)]
        public async Task Validate_WhenQuantityIsZeroOrLess_ShouldHaveInvalidMalingQuantityErrorCode(int quantity)
        {
            // Arrange
            var request = new MailingProductRequest
            {
                ProductId = Guid.NewGuid(),
                Quantity = quantity
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Quantity)
                  .WithErrorCode(ErrorCodes.InvalidMalingQuantity);
            await Task.CompletedTask;
        }

        // ─── Price ───────────────────────────────────────────────────────────

        [Test]
        public async Task Validate_WhenPriceIsNull_ShouldNotHaveValidationErrorForPrice()
        {
            // Arrange
            var request = new MailingProductRequest
            {
                ProductId = Guid.NewGuid(),
                Quantity = 1,
                Price = null
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Price);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenPriceIsGreaterThanZero_ShouldNotHaveValidationErrorForPrice()
        {
            // Arrange
            var request = new MailingProductRequest
            {
                ProductId = Guid.NewGuid(),
                Quantity = 1,
                Price = 100,
                CurrencyCode = "PLN"
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Price);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments(0)]
        [Arguments(-1)]
        public async Task Validate_WhenPriceIsZeroOrLess_ShouldHaveInvalidMalingPriceErrorCode(int price)
        {
            // Arrange
            var request = new MailingProductRequest
            {
                ProductId = Guid.NewGuid(),
                Quantity = 1,
                Price = price
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Price)
                  .WithErrorCode(ErrorCodes.InvalidMalingPrice);
            await Task.CompletedTask;
        }

        // ─── CurrencyCode ────────────────────────────────────────────────────

        [Test]
        public async Task Validate_WhenPriceHasValueAndCurrencyCodeIsNull_ShouldHaveCodeRequiredErrorCode()
        {
            // Arrange
            var request = new MailingProductRequest
            {
                ProductId = Guid.NewGuid(),
                Quantity = 1,
                Price = 100,
                CurrencyCode = null
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.CurrencyCode)
                  .WithErrorCode(ErrorCodes.CodeRequired);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenPriceHasValueAndCurrencyCodeIsProvided_ShouldNotHaveCodeRequiredValidationError()
        {
            // Arrange
            var request = new MailingProductRequest
            {
                ProductId = Guid.NewGuid(),
                Quantity = 1,
                Price = 100,
                CurrencyCode = "PLN"
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.CurrencyCode);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenPriceIsNullAndCurrencyCodeIsNull_ShouldNotHaveValidationErrorForCurrencyCode()
        {
            // Arrange
            var request = new MailingProductRequest
            {
                ProductId = Guid.NewGuid(),
                Quantity = 1,
                Price = null,
                CurrencyCode = null
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.CurrencyCode);
            await Task.CompletedTask;
        }
    }
}
