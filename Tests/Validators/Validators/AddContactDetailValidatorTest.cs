using Api.Request.Contact;
using Api.Validators.Validators.Contact;
using Domain.Constants;
using FluentValidation.TestHelper;

namespace Tests.Validators
{
    public class AddContactDetailValidatorTest
    {
        private readonly AddContactDetailValidator _validator = new();

        // ─── Type ────────────────────────────────────────────────────────────

        [Test]
        public async Task Validate_WhenTypeIsValid_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new AddContactDetailRequest
            {
                Type = "OTHER",
                Label = "Private",
                Value = "Some Value"
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Type);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments("")]
        [Arguments(" ")]
        [Arguments(null)]
        public async Task Validate_WhenTypeIsEmpty_ShouldHaveValidationError(string? type)
        {
            // Arrange
            var request = new AddContactDetailRequest
            {
                Type = type!,
                Label = "Private",
                Value = "Some Value"
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Type);
            await Task.CompletedTask;
        }

        // ─── Label ───────────────────────────────────────────────────────────

        [Test]
        public async Task Validate_WhenLabelIsValid_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new AddContactDetailRequest
            {
                Type = "OTHER",
                Label = "Private",
                Value = "Some Value"
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Label);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments("")]
        [Arguments(" ")]
        [Arguments(null)]
        public async Task Validate_WhenLabelIsEmpty_ShouldHaveLabelRequiredErrorCode(string? label)
        {
            // Arrange
            var request = new AddContactDetailRequest
            {
                Type = "OTHER",
                Label = label!,
                Value = "Some Value"
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Label)
                  .WithErrorCode(ErrorCodes.LabelRequired);
            await Task.CompletedTask;
        }

        // ─── Value (General & Conditional) ───────────────────────────────────

        [Test]
        [Arguments("")]
        [Arguments(" ")]
        [Arguments(null)]
        public async Task Validate_WhenValueIsEmptyForGeneralType_ShouldHaveContactValueRequiredErrorCode(string? value)
        {
            // Arrange
            var request = new AddContactDetailRequest
            {
                Type = "OTHER",
                Label = "Private",
                Value = value!
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Value)
                  .WithErrorCode(ErrorCodes.ContactValueRequired);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments("")]
        [Arguments(" ")]
        [Arguments(null)]
        public async Task Validate_WhenTypeIsEmailAndValueIsEmpty_ShouldHaveEmailRequiredErrorCode(string? value)
        {
            // Arrange
            var request = new AddContactDetailRequest
            {
                Type = "EMAIL",
                Label = "Private",
                Value = value!
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Value)
                  .WithErrorCode(ErrorCodes.EmailRequired);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments("")]
        [Arguments(" ")]
        [Arguments(null)]
        public async Task Validate_WhenTypeIsPhoneAndValueIsEmpty_ShouldHaveNumberRequiredErrorCode(string? value)
        {
            // Arrange
            var request = new AddContactDetailRequest
            {
                Type = "PHONE",
                Label = "Private",
                Value = value!
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Value)
                  .WithErrorCode(ErrorCodes.NumberRequired);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments("")]
        [Arguments(" ")]
        [Arguments(null)]
        public async Task Validate_WhenTypeIsFaxAndValueIsEmpty_ShouldHaveNumberRequiredErrorCode(string? value)
        {
            // Arrange
            var request = new AddContactDetailRequest
            {
                Type = "FAX",
                Label = "Private",
                Value = value!
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Value)
                  .WithErrorCode(ErrorCodes.NumberRequired);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments("")]
        [Arguments(" ")]
        [Arguments(null)]
        public async Task Validate_WhenTypeIsLinkedInAndValueIsEmpty_ShouldHaveLinkedInUrlRequiredErrorCode(string? value)
        {
            // Arrange
            var request = new AddContactDetailRequest
            {
                Type = "LINKEDIN",
                Label = "Private",
                Value = value!
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Value)
                  .WithErrorCode(ErrorCodes.LinkedInUrlRequired);
            await Task.CompletedTask;
        }
    }
}
