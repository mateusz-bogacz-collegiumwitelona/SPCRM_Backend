using Api.Request.Contact;
using Api.Validators.Validators.Contact;
using Domain.Constants;
using FluentValidation.TestHelper;

namespace Tests.Validators
{
    public class AddContactValidatorTest
    {
        private readonly AddContactValidator _validator = new();

        // ─── Details (IsPrimary Rule) ────────────────────────────────────────

        [Test]
        public async Task Validate_WhenDetailsHasExactlyOnePrimary_ShouldNotHaveValidationErrorForPrimaryRule()
        {
            // Arrange
            var request = new AddContactRequest
            {
                CompanyId = Guid.NewGuid(),
                FirstName = "John",
                LastName = "Doe",
                Details = new List<AddContactDetailRequest>
                {
                    new() { IsPrimary = true, Type = "EMAIL", Label = "Work", Value = "test@test.com" },
                    new() { IsPrimary = false, Type = "PHONE", Label = "Private", Value = "123456789" }
                }
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Details);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenDetailsHasNoPrimary_ShouldHavePrimaryContactDetailRequiredErrorCode()
        {
            // Arrange
            var request = new AddContactRequest
            {
                CompanyId = Guid.NewGuid(),
                FirstName = "John",
                LastName = "Doe",
                Details = new List<AddContactDetailRequest>
                {
                    new() { IsPrimary = false, Type = "EMAIL", Label = "Work", Value = "test@test.com" },
                    new() { IsPrimary = false, Type = "PHONE", Label = "Private", Value = "123456789" }
                }
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Details)
                  .WithErrorCode(ErrorCodes.PrimaryContactDetailRequired);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenDetailsHasMultiplePrimary_ShouldHavePrimaryContactDetailRequiredErrorCode()
        {
            // Arrange
            var request = new AddContactRequest
            {
                CompanyId = Guid.NewGuid(),
                FirstName = "John",
                LastName = "Doe",
                Details = new List<AddContactDetailRequest>
                {
                    new() { IsPrimary = true, Type = "EMAIL", Label = "Work", Value = "test@test.com" },
                    new() { IsPrimary = true, Type = "PHONE", Label = "Private", Value = "123456789" }
                }
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Details)
                  .WithErrorCode(ErrorCodes.PrimaryContactDetailRequired);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenDetailsIsEmpty_ShouldHaveValidationErrorCodeAndPrimaryRequiredErrorCode()
        {
            // Arrange
            var request = new AddContactRequest
            {
                CompanyId = Guid.NewGuid(),
                FirstName = "John",
                LastName = "Doe",
                Details = new List<AddContactDetailRequest>()
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Details)
                  .WithErrorCode(ErrorCodes.ValidationError);

            result.ShouldHaveValidationErrorFor(x => x.Details)
                  .WithErrorCode(ErrorCodes.PrimaryContactDetailRequired);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenDetailsIsNull_ShouldHaveValidationErrorCode()
        {
            // Arrange
            var request = new AddContactRequest
            {
                CompanyId = Guid.NewGuid(),
                FirstName = "John",
                LastName = "Doe",
                Details = null!
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Details)
                  .WithErrorCode(ErrorCodes.ValidationError);
            result.ShouldHaveValidationErrorFor(x => x.Details)
                  .WithErrorCode(ErrorCodes.PrimaryContactDetailRequired);
            await Task.CompletedTask;
        }
    }
}
