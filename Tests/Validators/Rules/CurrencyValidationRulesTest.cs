using Api.Validators.Rule;
using Domain.Constants;
using FluentValidation;
using FluentValidation.TestHelper;

namespace Tests.Validators.Rules
{
    public class CurrencyValidationRulesTest
    {
        private class TestCurrencyModel
        {
            public int DecimalPlaces { get; set; }
            public int? NullableDecimalPlaces { get; set; }
            public string? Code { get; set; }
            public string? Name { get; set; }
        }

        private class TestCurrencyModelValidator : AbstractValidator<TestCurrencyModel>
        {
            public TestCurrencyModelValidator()
            {
                RuleFor(x => x.DecimalPlaces).ApplyCurrencyDecimalPlacesRules();
                RuleFor(x => x.NullableDecimalPlaces).ApplyCurrencyDecimalPlacesRules();
                RuleFor(x => x.Code).ApplyCurrencyCodeRules();
                RuleFor(x => x.Name).ApplyCurrencyNameRules();
            }
        }

        private readonly TestCurrencyModelValidator _validator = new();

        // ─── ApplyCurrencyDecimalPlacesRules ─────────────────────────────────

        [Test]
        [Arguments(0)]
        [Arguments(2)]
        [Arguments(4)]
        public async Task ApplyCurrencyDecimalPlacesRules_WhenWithinRange_ShouldNotHaveValidationError(int validPlaces)
        {
            // Arrange
            var model = new TestCurrencyModel
            {
                DecimalPlaces = validPlaces,
                NullableDecimalPlaces = validPlaces
            };

            // Act
            var result = _validator.TestValidate(model, opt =>
            {
                opt.IncludeProperties(x => x.DecimalPlaces);
                opt.IncludeProperties(x => x.NullableDecimalPlaces);
            });

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.DecimalPlaces);
            result.ShouldNotHaveValidationErrorFor(x => x.NullableDecimalPlaces);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments(-1)]
        [Arguments(5)]
        [Arguments(10)]
        public async Task ApplyCurrencyDecimalPlacesRules_WhenOutOfRange_ShouldHaveDecimalPlacesInvalidErrorCode(int invalidPlaces)
        {
            // Arrange
            var model = new TestCurrencyModel
            {
                DecimalPlaces = invalidPlaces,
                NullableDecimalPlaces = invalidPlaces
            };

            // Act
            var result = _validator.TestValidate(model, opt =>
            {
                opt.IncludeProperties(x => x.DecimalPlaces);
                opt.IncludeProperties(x => x.NullableDecimalPlaces);
            });

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.DecimalPlaces)
                  .WithErrorCode(ErrorCodes.DecimalPlacesInvalid);
            result.ShouldHaveValidationErrorFor(x => x.NullableDecimalPlaces)
                  .WithErrorCode(ErrorCodes.DecimalPlacesInvalid);
            await Task.CompletedTask;
        }

        // ─── ApplyCurrencyCodeRules ──────────────────────────────────────────

        [Test]
        [Arguments("PLN")]
        [Arguments("EUR")]
        [Arguments("USD")]
        [Arguments("gbp")]
        public async Task ApplyCurrencyCodeRules_WhenValidCode_ShouldNotHaveValidationError(string validCode)
        {
            // Arrange
            var model = new TestCurrencyModel { Code = validCode };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Code));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Code);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments("")]
        [Arguments("   ")]
        [Arguments(null)]
        public async Task ApplyCurrencyCodeRules_WhenEmpty_ShouldHaveCodeRequiredErrorCode(string? emptyCode)
        {
            // Arrange
            var model = new TestCurrencyModel { Code = emptyCode };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Code));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Code)
                  .WithErrorCode(ErrorCodes.CodeRequired);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments("PL")]
        [Arguments("PLNN")]
        [Arguments("123")]
        [Arguments("P1N")]
        [Arguments("P-N")]
        public async Task ApplyCurrencyCodeRules_WhenInvalidFormat_ShouldHaveCodeFormatInvalidErrorCode(string invalidFormatCode)
        {
            // Arrange
            var model = new TestCurrencyModel { Code = invalidFormatCode };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Code));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Code)
                  .WithErrorCode(ErrorCodes.CodeFormatInvalid);
            await Task.CompletedTask;
        }

        // ─── ApplyCurrencyNameRules ──────────────────────────────────────────

        [Test]
        [Arguments("Polski Złoty")]
        [Arguments("Euro")]
        public async Task ApplyCurrencyNameRules_WhenValidName_ShouldNotHaveValidationError(string validName)
        {
            // Arrange
            var model = new TestCurrencyModel { Name = validName };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Name));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Name);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments("")]
        [Arguments("   ")]
        [Arguments(null)]
        public async Task ApplyCurrencyNameRules_WhenEmpty_ShouldHaveNameRequiredErrorCode(string? emptyName)
        {
            // Arrange
            var model = new TestCurrencyModel { Name = emptyName };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Name));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Name)
                  .WithErrorCode(ErrorCodes.NameRequired);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplyCurrencyNameRules_WhenExceeds100Characters_ShouldHaveNameLengthInvalidErrorCode()
        {
            // Arrange
            var model = new TestCurrencyModel { Name = new string('A', 101) };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Name));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Name)
                  .WithErrorCode(ErrorCodes.NameLengthInvalid);
            await Task.CompletedTask;
        }
    }
}
