using Api.Validators.Rule;
using Domain.Constants;
using Domain.Enum;
using FluentValidation;
using FluentValidation.TestHelper;

namespace Tests.Validators.Rules
{
    public class ContactValidationRulesTest
    {
        private class TestContactModel
        {
            public string? Name { get; set; }
            public string? Type { get; set; }
            public string? Label { get; set; }
            public string? Email { get; set; }
            public string? Phone { get; set; }
            public string? Fax { get; set; }
            public string? LinkedIn { get; set; }
        }

        private class TestContactModelValidator : AbstractValidator<TestContactModel>
        {
            public TestContactModelValidator()
            {
                RuleFor(x => x.Name).ApplyNameRules();
                RuleFor(x => x.Type).ApplyTypeRules();
                RuleFor(x => x.Label).ApplyLabelRules();
                RuleFor(x => x.Email).ApplyEmailRules();
                RuleFor(x => x.Phone).ApplyPhoneRules();
                RuleFor(x => x.Fax).ApplyFaxRules();
                RuleFor(x => x.LinkedIn).ApplyLinkedInRules();
            }
        }

        private readonly TestContactModelValidator _validator = new();

        // ─── ApplyNameRules ──────────────────────────────────────────────────

        [Test]
        [Arguments("Jan")]
        [Arguments("A")]
        public async Task ApplyNameRules_WhenValidLength_ShouldNotHaveValidationError(string validName)
        {
            // Arrange
            var model = new TestContactModel { Name = validName };

            // Act
            var result = _validator.TestValidate(model);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Name);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments("")]
        public async Task ApplyNameRules_WhenEmptyOrExceeds100Chars_ShouldHaveNameLengthInvalidErrorCode(string invalidName)
        {
            // Arrange
            var model = new TestContactModel { Name = invalidName };

            // Act
            var result = _validator.TestValidate(model);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Name)
                  .WithErrorCode(ErrorCodes.NameLengthInvalid);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplyNameRules_WhenExceeds100Characters_ShouldHaveNameLengthInvalidErrorCode()
        {
            // Arrange
            var model = new TestContactModel { Name = new string('A', 101) };

            // Act
            var result = _validator.TestValidate(model);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Name)
                  .WithErrorCode(ErrorCodes.NameLengthInvalid);
            await Task.CompletedTask;
        }

        // ─── ApplyTypeRules ──────────────────────────────────────────────────

        [Test]
        public async Task ApplyTypeRules_WhenValidEnumName_ShouldNotHaveValidationError()
        {
            // Arrange
            var validNames = Enum.GetNames<ContactDetailTypeEnum>();

            foreach (var type in validNames)
            {
                var modelExact = new TestContactModel { Type = type };
                var modelLower = new TestContactModel { Type = type.ToLower() };

                // Act
                var resultExact = _validator.TestValidate(modelExact);
                var resultLower = _validator.TestValidate(modelLower);

                // Assert
                resultExact.ShouldNotHaveValidationErrorFor(x => x.Type);
                resultLower.ShouldNotHaveValidationErrorFor(x => x.Type);
            }

            await Task.CompletedTask;
        }

        [Test]
        [Arguments("NieznanyTyp")]
        [Arguments("Invalid")]
        [Arguments("123")]
        [Arguments("")]
        [Arguments("   ")]
        public async Task ApplyTypeRules_WhenInvalidEnumName_ShouldHaveTypeInvalidErrorCode(string invalidType)
        {
            // Arrange
            var model = new TestContactModel { Type = invalidType };

            // Act
            var result = _validator.TestValidate(model);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Type)
                  .WithErrorCode(ErrorCodes.TypeInvalid);
            await Task.CompletedTask;
        }

        // ─── ApplyLabelRules ─────────────────────────────────────────────────

        [Test]
        [Arguments("Biuro")]
        [Arguments("Telefon prywatny")]
        public async Task ApplyLabelRules_WhenValidLength_ShouldNotHaveValidationError(string validLabel)
        {
            // Arrange
            var model = new TestContactModel { Label = validLabel };

            // Act
            var result = _validator.TestValidate(model);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Label);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments("")]
        public async Task ApplyLabelRules_WhenEmpty_ShouldHaveLabelLengthInvalidErrorCode(string emptyLabel)
        {
            // Arrange
            var model = new TestContactModel { Label = emptyLabel };

            // Act
            var result = _validator.TestValidate(model);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Label)
                  .WithErrorCode(ErrorCodes.LabelLengthInvalid);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplyLabelRules_WhenExceeds50Characters_ShouldHaveLabelLengthInvalidErrorCode()
        {
            // Arrange
            var model = new TestContactModel { Label = new string('L', 51) };

            // Act
            var result = _validator.TestValidate(model);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Label)
                  .WithErrorCode(ErrorCodes.LabelLengthInvalid);
            await Task.CompletedTask;
        }

        // ─── ApplyEmailRules ─────────────────────────────────────────────────

        [Test]
        [Arguments("test@spcrm.pl")]
        [Arguments("jan.kowalski@firma.com")]
        [Arguments("admin+test@domain.org")]
        public async Task ApplyEmailRules_WhenValidEmailFormat_ShouldNotHaveValidationError(string validEmail)
        {
            // Arrange
            var model = new TestContactModel { Email = validEmail };

            // Act
            var result = _validator.TestValidate(model);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Email);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments("brak_znaku_malpy")]
        [Arguments("@domena.pl")]
        [Arguments("uzytkownik@")]
        [Arguments("dwie@@malpy.pl")]
        public async Task ApplyEmailRules_WhenInvalidEmailFormat_ShouldHaveEmailInvalidErrorCode(string invalidEmail)
        {
            // Arrange
            var model = new TestContactModel { Email = invalidEmail };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Email));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Email)
                  .WithErrorCode(ErrorCodes.EmailInvalid);
            await Task.CompletedTask;
        }

        // ─── ApplyPhoneRules ─────────────────────────────────────────────────

        [Test]
        [Arguments("+48 123 456 789")]
        [Arguments("123-456-789")]
        [Arguments("(22) 123 45 67")]
        [Arguments("+48123456789")]
        [Arguments("1234567")]
        public async Task ApplyPhoneRules_WhenValidPhoneNumber_ShouldNotHaveValidationError(string validPhone)
        {
            // Arrange
            var model = new TestContactModel { Phone = validPhone };

            // Act
            var result = _validator.TestValidate(model);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Phone);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments("123456")]
        [Arguments("1234567890123456")]
        [Arguments("+48 123 456 ABC")]
        [Arguments("telefon: 123456789")]
        [Arguments("")]
        [Arguments("   ")]
        [Arguments(null)]
        public async Task ApplyPhoneRules_WhenInvalidPhoneNumber_ShouldHaveNumberInvalidErrorCode(string? invalidPhone)
        {
            // Arrange
            var model = new TestContactModel { Phone = invalidPhone };

            // Act
            var result = _validator.TestValidate(model);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Phone)
                  .WithErrorCode(ErrorCodes.NumberInvalid);
            await Task.CompletedTask;
        }

        // ─── ApplyFaxRules ───────────────────────────────────────────────────

        [Test]
        [Arguments("+48 22 123 45 67")]
        [Arguments("(12) 345-67-89")]
        public async Task ApplyFaxRules_WhenValidFaxNumber_ShouldNotHaveValidationError(string validFax)
        {
            // Arrange
            var model = new TestContactModel { Fax = validFax };

            // Act
            var result = _validator.TestValidate(model);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Fax);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments("123")]
        [Arguments("fax_number")]
        [Arguments("")]
        [Arguments(null)]
        public async Task ApplyFaxRules_WhenInvalidFaxNumber_ShouldHaveFaxInvalidErrorCode(string? invalidFax)
        {
            // Arrange
            var model = new TestContactModel { Fax = invalidFax };

            // Act
            var result = _validator.TestValidate(model);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Fax)
                  .WithErrorCode(ErrorCodes.FaxInvalid);
            await Task.CompletedTask;
        }

        // ─── ApplyLinkedInRules ──────────────────────────────────────────────

        [Test]
        [Arguments("https://www.linkedin.com/in/jan-kowalski")]
        [Arguments("http://linkedin.com/company/spcrm")]
        [Arguments("https://pl.linkedin.com/in/profile")]
        public async Task ApplyLinkedInRules_WhenValidLinkedInUrl_ShouldNotHaveValidationError(string validUrl)
        {
            // Arrange
            var model = new TestContactModel { LinkedIn = validUrl };

            // Act
            var result = _validator.TestValidate(model);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.LinkedIn);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments("https://facebook.com/profil")]
        [Arguments("https://goldenline.pl/profil")]
        [Arguments("ftp://linkedin.com/in/user")]
        [Arguments("nie-jest-url")]
        [Arguments("www.linkedin.com/in/user")]
        [Arguments("")]
        [Arguments("   ")]
        [Arguments(null)]
        public async Task ApplyLinkedInRules_WhenInvalidLinkedInUrl_ShouldHaveLinkedInUrlInvalidErrorCode(string? invalidUrl)
        {
            // Arrange
            var model = new TestContactModel { LinkedIn = invalidUrl };

            // Act
            var result = _validator.TestValidate(model);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.LinkedIn)
                  .WithErrorCode(ErrorCodes.LinkedInUrlInvalid);
            await Task.CompletedTask;
        }

        // ─── IsPhoneType ─────────────────────────────────────────────────────

        [Test]
        [Arguments("PHONE", true)]
        [Arguments("phone", true)]
        [Arguments("PHONE_MOBILE", true)]
        [Arguments("phone_mobile", true)]
        [Arguments("EMAIL", false)]
        [Arguments("FAX", false)]
        [Arguments("", false)]
        [Arguments(null, false)]
        public async Task IsPhoneType_WhenEvaluated_ReturnsExpectedBoolean(string? type, bool expected)
        {
            // Arrange & Act
            var result = ContactValidationRules.IsPhoneType(type);

            // Assert
            await Assert.That(result).IsEqualTo(expected);
        }

        // ─── IsFaxType ───────────────────────────────────────────────────────

        [Test]
        [Arguments("FAX", true)]
        [Arguments("fax", true)]
        [Arguments("Fax", true)]
        [Arguments("PHONE", false)]
        [Arguments("EMAIL", false)]
        [Arguments("", false)]
        [Arguments(null, false)]
        public async Task IsFaxType_WhenEvaluated_ReturnsExpectedBoolean(string? type, bool expected)
        {
            // Arrange & Act
            var result = ContactValidationRules.IsFaxType(type);

            // Assert
            await Assert.That(result).IsEqualTo(expected);
        }
    }
}
