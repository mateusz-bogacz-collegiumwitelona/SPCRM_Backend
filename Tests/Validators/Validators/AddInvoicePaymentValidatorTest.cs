using Api.Request.Invoice;
using Api.Validators.Validators.Invoice;
using Domain.Constants;
using FluentValidation.TestHelper;

namespace Tests.Validators
{
    public class AddInvoicePaymentValidatorTest
    {
        private readonly AddInvoicePaymentValidator _validator = new();

        // ─── Amount ──────────────────────────────────────────────────────────

        [Test]
        public async Task Validate_WhenAmountIsGreaterThanZero_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new AddInvoicePaymentRequest
            {
                Amount = 100,
                PaymentDate = DateTime.UtcNow
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Amount);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments(0)]
        [Arguments(-1)]
        public async Task Validate_WhenAmountIsZeroOrLess_ShouldHaveInvoicePaymentInvalidErrorCode(int amount)
        {
            // Arrange
            var request = new AddInvoicePaymentRequest
            {
                Amount = amount,
                PaymentDate = DateTime.UtcNow
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Amount)
                  .WithErrorCode(ErrorCodes.InvoicePaymentInvalid);
            await Task.CompletedTask;
        }

        // ─── PaymentDate ─────────────────────────────────────────────────────

        [Test]
        public async Task Validate_WhenPaymentDateIsValid_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new AddInvoicePaymentRequest
            {
                Amount = 100,
                PaymentDate = DateTime.UtcNow
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.PaymentDate);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenPaymentDateIsEmpty_ShouldHavePaymentDateRequiredErrorCode()
        {
            // Arrange
            var request = new AddInvoicePaymentRequest
            {
                Amount = 100,
                PaymentDate = default
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.PaymentDate)
                  .WithErrorCode(ErrorCodes.PaymentDateRequired);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenPaymentDateIsInFutureBeyondLimit_ShouldHaveInvalidDateErrorCode()
        {
            // Arrange
            var request = new AddInvoicePaymentRequest
            {
                Amount = 100,
                PaymentDate = DateTime.UtcNow.AddMinutes(6)
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.PaymentDate)
                  .WithErrorCode(ErrorCodes.InvalidDate);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenPaymentDateIsBefore2020_ShouldHaveInvalidDateErrorCode()
        {
            // Arrange
            var request = new AddInvoicePaymentRequest
            {
                Amount = 100,
                PaymentDate = new DateTime(2019, 12, 31, 23, 59, 59, DateTimeKind.Utc)
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.PaymentDate)
                  .WithErrorCode(ErrorCodes.InvalidDate);
            await Task.CompletedTask;
        }

        // ─── ReferenceNumber ─────────────────────────────────────────────────

        [Test]
        public async Task Validate_WhenReferenceNumberIsValidLength_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new AddInvoicePaymentRequest
            {
                Amount = 100,
                PaymentDate = DateTime.UtcNow,
                ReferenceNumber = new string('A', 100)
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.ReferenceNumber);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenReferenceNumberIsTooLong_ShouldHavePaymentReferenceNumberInvalidErrorCode()
        {
            // Arrange
            var request = new AddInvoicePaymentRequest
            {
                Amount = 100,
                PaymentDate = DateTime.UtcNow,
                ReferenceNumber = new string('A', 101)
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.ReferenceNumber)
                  .WithErrorCode(ErrorCodes.PaymentReferenceNumberInvalid);
            await Task.CompletedTask;
        }

        // ─── Note ────────────────────────────────────────────────────────────

        [Test]
        public async Task Validate_WhenNoteIsValidLength_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new AddInvoicePaymentRequest
            {
                Amount = 100,
                PaymentDate = DateTime.UtcNow,
                Note = new string('A', 500)
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Note);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenNoteIsTooLong_ShouldHavePaymentNoteInvalidErrorCode()
        {
            // Arrange
            var request = new AddInvoicePaymentRequest
            {
                Amount = 100,
                PaymentDate = DateTime.UtcNow,
                Note = new string('A', 501)
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Note)
                  .WithErrorCode(ErrorCodes.PaymentNoteInvalid);
            await Task.CompletedTask;
        }
    }
}
