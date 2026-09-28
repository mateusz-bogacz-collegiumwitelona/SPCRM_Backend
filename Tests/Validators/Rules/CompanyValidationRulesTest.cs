using Api.Validators.Rule;
using Domain.Constants;
using Domain.Enum;
using FluentValidation;
using FluentValidation.TestHelper;

namespace Tests.Validators.Rules
{
    public class CompanyValidationRulesTest
    {

        private class TestCompanyModel
        {
            public string? Name { get; set; }
            public string? Street { get; set; }
            public string? City { get; set; }
            public string? Nip { get; set; }
            public string? ZipCode { get; set; }
            public float Latitude { get; set; }
            public float Longitude { get; set; }
            public float? NullableLatitude { get; set; }
            public float? NullableLongitude { get; set; }
            public string? AddressType { get; set; }
        }

        private class TestCompanyModelValidator : AbstractValidator<TestCompanyModel>
        {
            public TestCompanyModelValidator()
            {
                RuleFor(x => x.Name).ApplyCompanyNameRules();
                RuleFor(x => x.Street).ApplyCompanyStreetRules();
                RuleFor(x => x.City).ApplyCompanyCityRules();
                RuleFor(x => x.Nip).ApplyCompanyNipRules();
                RuleFor(x => x.ZipCode).ApplyCompanyZipCodeRules();
                RuleFor(x => x.Latitude).ApplyCompanyLatitudeRules();
                RuleFor(x => x.Longitude).ApplyCompanyLongitudeRules();
                RuleFor(x => x.NullableLatitude).ApplyCompanyLatitudeRules();
                RuleFor(x => x.NullableLongitude).ApplyCompanyLongitudeRules();
                RuleFor(x => x.AddressType).ApplyCompanyAddressTypeRules();
            }
        }

        private readonly TestCompanyModelValidator _validator = new();

        // ─── NIP ─────────────────────────────────────────────────────────────

        [Test]
        [Arguments("7740001454")]
        [Arguments("5260250995")]
        [Arguments("PL 774-000-14-54")]
        [Arguments("526 025 09 95")]
        public async Task NipRule_WhenNipIsValid_ShouldNotHaveValidationError(string validNip)
        {
            // Arrange
            var model = new TestCompanyModel { Nip = validNip };

            // Act
            var result = _validator.TestValidate(model);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Nip);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments("1234567890")]
        [Arguments("7740001455")]
        [Arguments("123456789")]
        [Arguments("12345678901")]
        [Arguments("PL123456789")]
        [Arguments("ABC4567890")]
        public async Task NipRule_WhenNipChecksumIsInvalid_ShouldHaveNipNotValidErrorCode(string invalidNip)
        {
            // Arrange
            var model = new TestCompanyModel { Nip = invalidNip };

            // Act
            var result = _validator.TestValidate(model);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Nip)
                  .WithErrorCode(ErrorCodes.NipNotValid);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments("")]
        [Arguments("   ")]
        [Arguments(null)]
        public async Task NipRule_WhenNipIsEmpty_ShouldHaveNipRequiredErrorCode(string? emptyNip)
        {
            // Arrange
            var model = new TestCompanyModel { Nip = emptyNip };

            // Act
            var result = _validator.TestValidate(model);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Nip)
                  .WithErrorCode(ErrorCodes.NipRequired);
            await Task.CompletedTask;
        }

        // ─── ZipCodeRule ──────────────────────────────────────────

        [Test]
        [Arguments("00-950")]
        [Arguments("40-001")]
        public async Task ZipCodeRule_WhenValidFormat_ShouldNotHaveValidationError(string validZip)
        {
            // Arrange
            var model = new TestCompanyModel { ZipCode = validZip };

            // Act
            var result = _validator.TestValidate(model);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.ZipCode);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments("00950")]
        [Arguments("0-950")]
        [Arguments("00-9500")]
        [Arguments("AA-000")]
        public async Task ZipCodeRule_WhenInvalidFormat_ShouldHaveZipCodeNotValidErrorCode(string invalidZip)
        {
            // Arrange
            var model = new TestCompanyModel { ZipCode = invalidZip };

            // Act
            var result = _validator.TestValidate(model);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.ZipCode)
                  .WithErrorCode(ErrorCodes.ZipCodeNotValid);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments("")]
        [Arguments("   ")]
        [Arguments(null)]
        public async Task ZipCodeRule_WhenEmpty_ShouldHaveZipCodeRequiredErrorCode(string? emptyZip)
        {
            // Arrange
            var model = new TestCompanyModel { ZipCode = emptyZip };

            // Act
            var result = _validator.TestValidate(model);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.ZipCode)
                  .WithErrorCode(ErrorCodes.ZipCodeRequired);
            await Task.CompletedTask;
        }

        // ─── CompanyNameRule ───────────────────────────────────────

        [Test]
        public async Task CompanyNameRule_WhenValidLength_ShouldNotHaveValidationError()
        {
            // Arrange
            var model = new TestCompanyModel { Name = "Stal-Bud Sp. z o.o." };

            // Act
            var result = _validator.TestValidate(model);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Name);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments("")]
        [Arguments("   ")]
        [Arguments(null)]
        public async Task CompanyNameRule_WhenEmpty_ShouldHaveNameRequiredErrorCode(string? emptyName)
        {
            // Arrange
            var model = new TestCompanyModel { Name = emptyName };

            // Act
            var result = _validator.TestValidate(model);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Name)
                  .WithErrorCode(ErrorCodes.NameRequired);
            await Task.CompletedTask;
        }

        [Test]
        public async Task CompanyNameRule_WhenExceeds100Characters_ShouldHaveNameLengthInvalidErrorCode()
        {
            // Arrange
            var model = new TestCompanyModel { Name = new string('A', 101) };

            // Act
            var result = _validator.TestValidate(model);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Name)
                  .WithErrorCode(ErrorCodes.NameLengthInvalid);
            await Task.CompletedTask;
        }

        // ─── StreetRule ──────────────────────────────────

        [Test]
        public async Task StreetRule_WhenExceeds200Characters_ShouldHaveStreetLengthInvalidErrorCode()
        {
            // Arrange
            var model = new TestCompanyModel { Street = new string('S', 201) };

            // Act
            var result = _validator.TestValidate(model);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Street)
                  .WithErrorCode(ErrorCodes.StreetLengthInvalid);
            await Task.CompletedTask;
        }

        // ─── CityRule ──────────────────────────────────


        [Test]
        public async Task CityRule_WhenExceeds100Characters_ShouldHaveCityLengthInvalidErrorCode()
        {
            // Arrange
            var model = new TestCompanyModel { City = new string('C', 101) };

            // Act
            var result = _validator.TestValidate(model);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.City)
                  .WithErrorCode(ErrorCodes.CityLengthInvalid);
            await Task.CompletedTask;
        }

        // ─── LatitudeRule ──────────────────────────

        [Test]
        [Arguments(-90f)]
        [Arguments(0f)]
        [Arguments(50.2649f)]
        [Arguments(90f)]
        public async Task LatitudeRule_WhenWithinRange_ShouldNotHaveValidationError(float validLatitude)
        {
            // Arrange
            var model = new TestCompanyModel { Latitude = validLatitude, NullableLatitude = validLatitude };

            // Act
            var result = _validator.TestValidate(model);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Latitude);
            result.ShouldNotHaveValidationErrorFor(x => x.NullableLatitude);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments(-90.01f)]
        [Arguments(90.01f)]
        [Arguments(150f)]
        public async Task LatitudeRule_WhenOutOfRange_ShouldHaveLatitudeOutOfRangeErrorCode(float invalidLatitude)
        {
            // Arrange
            var model = new TestCompanyModel { Latitude = invalidLatitude, NullableLatitude = invalidLatitude };

            // Act
            var result = _validator.TestValidate(model);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Latitude)
                  .WithErrorCode(ErrorCodes.LatitudeOutOfRange);
            result.ShouldHaveValidationErrorFor(x => x.NullableLatitude)
                  .WithErrorCode(ErrorCodes.LatitudeOutOfRange);
            await Task.CompletedTask;
        }

        // ─── LongitudeRule ──────────────────────────

        [Test]
        [Arguments(-180f)]
        [Arguments(0f)]
        [Arguments(19.0238f)]
        [Arguments(180f)]
        public async Task LongitudeRule_WhenWithinRange_ShouldNotHaveValidationError(float validLongitude)
        {
            // Arrange
            var model = new TestCompanyModel { Longitude = validLongitude, NullableLongitude = validLongitude };

            // Act
            var result = _validator.TestValidate(model);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Longitude);
            result.ShouldNotHaveValidationErrorFor(x => x.NullableLongitude);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments(-180.01f)]
        [Arguments(180.01f)]
        [Arguments(250f)]
        public async Task LongitudeRule_WhenOutOfRange_ShouldHaveLongitudeOutOfRangeErrorCode(float invalidLongitude)
        {
            // Arrange
            var model = new TestCompanyModel { Longitude = invalidLongitude, NullableLongitude = invalidLongitude };

            // Act
            var result = _validator.TestValidate(model);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Longitude)
                  .WithErrorCode(ErrorCodes.LongitudeOutOfRange);
            result.ShouldHaveValidationErrorFor(x => x.NullableLongitude)
                  .WithErrorCode(ErrorCodes.LongitudeOutOfRange);
            await Task.CompletedTask;
        }

        // ─── AddressTypeRule ────────────────────────────────────────

        [Test]
        public async Task AddressTypeRule_WhenValidEnumName_ShouldNotHaveValidationError()
        {
            // Arrange
            var validName = nameof(AddressTypeEnum.Headquarters);
            var modelExact = new TestCompanyModel { AddressType = validName };
            var modelLower = new TestCompanyModel { AddressType = validName.ToLower() };

            // Act
            var resultExact = _validator.TestValidate(modelExact);
            var resultLower = _validator.TestValidate(modelLower);

            // Assert
            resultExact.ShouldNotHaveValidationErrorFor(x => x.AddressType);
            resultLower.ShouldNotHaveValidationErrorFor(x => x.AddressType);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments("NieznanyTyp")]
        [Arguments("Invalid")]
        [Arguments("123")]
        public async Task AddressTypeRule_WhenInvalidEnumName_ShouldHaveAddressTypeNotInvalidErrorCode(string invalidType)
        {
            // Arrange
            var model = new TestCompanyModel { AddressType = invalidType };

            // Act
            var result = _validator.TestValidate(model);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.AddressType)
                  .WithErrorCode(ErrorCodes.AddressTypeNotInvalid);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments("")]
        [Arguments("   ")]
        [Arguments(null)]
        public async Task AddressTypeRule_WhenEmpty_ShouldHaveAddressTypeRequiredErrorCode(string? emptyType)
        {
            // Arrange
            var model = new TestCompanyModel { AddressType = emptyType };

            // Act
            var result = _validator.TestValidate(model);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.AddressType)
                  .WithErrorCode(ErrorCodes.AddressTypeRequired);
            await Task.CompletedTask;
        }
    }
}
