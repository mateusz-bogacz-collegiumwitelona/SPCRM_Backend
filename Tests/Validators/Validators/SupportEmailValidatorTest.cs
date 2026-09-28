using Api.Request.Support;
using Api.Validators.Validators.Support;
using Domain.Constants;
using FluentValidation.TestHelper;

namespace Tests.Validators
{
    public class SupportEmailValidatorTest
    {
        private readonly SupportEmailValidator _validator = new();

        // ─── Email ───────────────────────────────────────────────────────────

        [Test]
        public async Task Validate_WhenEmailIsValid_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new SupportEmailRequest
            {
                Email = "user@example.com",
                Title = "Valid Title",
                Message = "Valid message content."
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Email);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenEmailIsEmpty_ShouldHaveEmailRequiredErrorCode()
        {
            // Arrange
            var request = new SupportEmailRequest
            {
                Email = string.Empty,
                Title = "Valid Title",
                Message = "Valid message content."
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Email)
                  .WithErrorCode(ErrorCodes.EmailRequired);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenEmailIsInvalid_ShouldHaveEmailInvalidErrorCode()
        {
            // Arrange
            var request = new SupportEmailRequest
            {
                Email = "invalid-email-format",
                Title = "Valid Title",
                Message = "Valid message content."
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Email)
                  .WithErrorCode(ErrorCodes.EmailInvalid);
            await Task.CompletedTask;
        }

        // ─── Title ───────────────────────────────────────────────────────────

        [Test]
        public async Task Validate_WhenTitleIsValid_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new SupportEmailRequest
            {
                Email = "user@example.com",
                Title = "Valid Title",
                Message = "Valid message content."
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Title);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenTitleIsEmpty_ShouldHaveTitleRequiredErrorCode()
        {
            // Arrange
            var request = new SupportEmailRequest
            {
                Email = "user@example.com",
                Title = string.Empty,
                Message = "Valid message content."
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Title)
                  .WithErrorCode(ErrorCodes.TitleRequired);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenTitleIsTooShort_ShouldHaveTitleLengthInvalidErrorCode()
        {
            // Arrange
            var request = new SupportEmailRequest
            {
                Email = "user@example.com",
                Title = "abc",
                Message = "Valid message content."
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Title)
                  .WithErrorCode(ErrorCodes.TitleLengthInvalid);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenTitleIsTooLong_ShouldHaveTitleLengthInvalidErrorCode()
        {
            // Arrange
            var request = new SupportEmailRequest
            {
                Email = "user@example.com",
                Title = new string('A', 101),
                Message = "Valid message content."
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Title)
                  .WithErrorCode(ErrorCodes.TitleLengthInvalid);
            await Task.CompletedTask;
        }

        // ─── Message ─────────────────────────────────────────────────────────

        [Test]
        public async Task Validate_WhenMessageIsValid_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new SupportEmailRequest
            {
                Email = "user@example.com",
                Title = "Valid Title",
                Message = "Valid message content."
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Message);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenMessageIsEmpty_ShouldHaveMessageRequiredErrorCode()
        {
            // Arrange
            var request = new SupportEmailRequest
            {
                Email = "user@example.com",
                Title = "Valid Title",
                Message = string.Empty
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Message)
                  .WithErrorCode(ErrorCodes.MessageRequired);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenMessageIsTooShort_ShouldHaveMessageLengthInvalidErrorCode()
        {
            // Arrange
            var request = new SupportEmailRequest
            {
                Email = "user@example.com",
                Title = "Valid Title",
                Message = "1234"
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Message)
                  .WithErrorCode(ErrorCodes.MessageLengthInvalid);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenMessageIsTooLong_ShouldHaveMessageLengthInvalidErrorCode()
        {
            // Arrange
            var request = new SupportEmailRequest
            {
                Email = "user@example.com",
                Title = "Valid Title",
                Message = new string('A', 5001)
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Message)
                  .WithErrorCode(ErrorCodes.MessageLengthInvalid);
            await Task.CompletedTask;
        }
    }
}
