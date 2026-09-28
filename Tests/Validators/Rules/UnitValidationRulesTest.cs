using Api.Validators.Rule;
using Domain.Constants;
using FluentValidation;
using FluentValidation.TestHelper;

namespace Tests.Validators.Rules
{
    public class UnitValidationRulesTest
    {
        private class TestUnitModel
        {
            public string? Name { get; set; }
            public string? Symbol { get; set; }
            public int BaseMultiplier { get; set; }
            public int? NullableBaseMultiplier { get; set; }
        }

        private class TestUnitModelValidator : AbstractValidator<TestUnitModel>
        {
            public TestUnitModelValidator()
            {
                RuleFor(x => x.Name).ApplyUnitNameRules();
                RuleFor(x => x.Symbol).ApplyUnitSymbolRules();
                RuleFor(x => x.BaseMultiplier).ApplyUnitBaseMultiplierRules();
                RuleFor(x => x.NullableBaseMultiplier).ApplyUnitBaseMultiplierRules();
            }
        }

        private readonly TestUnitModelValidator _validator = new();

        // ─── ApplyUnitNameRules ──────────────────────────────────────────────

        [Test]
        [Arguments("Sztuka")]
        [Arguments("Metr bieżący")]
        [Arguments("A")]
        public async Task ApplyUnitNameRules_WhenValidName_ShouldNotHaveValidationError(string validName)
        {
            // Arrange
            var model = new TestUnitModel { Name = validName };

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
        public async Task ApplyUnitNameRules_WhenEmptyOrNull_ShouldHaveNameRequiredErrorCode(string? emptyName)
        {
            // Arrange
            var model = new TestUnitModel { Name = emptyName };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Name));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Name)
                  .WithErrorCode(ErrorCodes.NameRequired);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplyUnitNameRules_WhenExceeds100Characters_ShouldHaveInvalidUnitNameErrorCode()
        {
            // Arrange
            var model = new TestUnitModel { Name = new string('U', 101) };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Name));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Name)
                  .WithErrorCode(ErrorCodes.InvalidUnitName);
            await Task.CompletedTask;
        }

        // ─── ApplyUnitSymbolRules ────────────────────────────────────────────

        [Test]
        [Arguments("szt.")]
        [Arguments("m")]
        [Arguments("kg")]
        public async Task ApplyUnitSymbolRules_WhenValidSymbol_ShouldNotHaveValidationError(string validSymbol)
        {
            // Arrange
            var model = new TestUnitModel { Symbol = validSymbol };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Symbol));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Symbol);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments("")]
        [Arguments("   ")]
        [Arguments(null)]
        public async Task ApplyUnitSymbolRules_WhenEmptyOrNull_ShouldHaveInvalidUnitSymbolErrorCode(string? emptySymbol)
        {
            // Arrange
            var model = new TestUnitModel { Symbol = emptySymbol };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Symbol));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Symbol)
                  .WithErrorCode(ErrorCodes.InvalidUnitSymbol);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplyUnitSymbolRules_WhenExceeds20Characters_ShouldHaveInvalidUnitSymbolErrorCode()
        {
            // Arrange
            var model = new TestUnitModel { Symbol = new string('S', 21) };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Symbol));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Symbol)
                  .WithErrorCode(ErrorCodes.InvalidUnitSymbol);
            await Task.CompletedTask;
        }

        // ─── ApplyUnitBaseMultiplierRules ────────────────────────────────────

        [Test]
        [Arguments(1)]
        [Arguments(1000)]
        public async Task ApplyUnitBaseMultiplierRules_WhenGreaterThanZero_ShouldNotHaveValidationError(int validMultiplier)
        {
            // Arrange
            var model = new TestUnitModel
            {
                BaseMultiplier = validMultiplier,
                NullableBaseMultiplier = validMultiplier
            };

            // Act
            var result = _validator.TestValidate(model, opt =>
            {
                opt.IncludeProperties(x => x.BaseMultiplier);
                opt.IncludeProperties(x => x.NullableBaseMultiplier);
            });

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.BaseMultiplier);
            result.ShouldNotHaveValidationErrorFor(x => x.NullableBaseMultiplier);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplyUnitBaseMultiplierRules_WhenNullableMultiplierIsNull_ShouldNotHaveValidationError()
        {
            // Arrange
            var model = new TestUnitModel { NullableBaseMultiplier = null };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.NullableBaseMultiplier));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.NullableBaseMultiplier);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments(0)]
        [Arguments(-1)]
        [Arguments(-100)]
        public async Task ApplyUnitBaseMultiplierRules_WhenZeroOrLess_ShouldHaveInvalidUnitBaseMultiplierErrorCode(int invalidMultiplier)
        {
            // Arrange
            var model = new TestUnitModel
            {
                BaseMultiplier = invalidMultiplier,
                NullableBaseMultiplier = invalidMultiplier
            };

            // Act
            var result = _validator.TestValidate(model, opt =>
            {
                opt.IncludeProperties(x => x.BaseMultiplier);
                opt.IncludeProperties(x => x.NullableBaseMultiplier);
            });

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.BaseMultiplier)
                  .WithErrorCode(ErrorCodes.InvalidUnitBaseMultiplier);
            result.ShouldHaveValidationErrorFor(x => x.NullableBaseMultiplier)
                  .WithErrorCode(ErrorCodes.InvalidUnitBaseMultiplier);
            await Task.CompletedTask;
        }
    }
}
