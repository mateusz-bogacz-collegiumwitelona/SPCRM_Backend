using Api.Validators.Rule;
using Domain.Constants;
using FluentValidation;
using FluentValidation.TestHelper;

namespace Tests.Validators.Rules
{
    public class SteelGradeValidationRulesTest
    {
        private class TestSteelGradeModel
        {
            public string? Name { get; set; }
            public string? Standard { get; set; }
            public decimal Density { get; set; }
            public decimal? NullableDensity { get; set; }
        }

        private class TestSteelGradeModelValidator : AbstractValidator<TestSteelGradeModel>
        {
            public TestSteelGradeModelValidator()
            {
                RuleFor(x => x.Name).ApplySteelGradeNameRules();
                RuleFor(x => x.Standard).ApplySteelGradeStandardRules();
                RuleFor(x => x.Density).ApplySteelGradeDensityRules();
                RuleFor(x => x.NullableDensity).ApplySteelGradeDensityRules();
            }
        }

        private readonly TestSteelGradeModelValidator _validator = new();

        // ─── ApplySteelGradeNameRules ────────────────────────────────────────

        [Test]
        [Arguments("1.4301")]
        [Arguments("S355J2")]
        [Arguments("A")]
        public async Task ApplySteelGradeNameRules_WhenValidName_ShouldNotHaveValidationError(string validName)
        {
            // Arrange
            var model = new TestSteelGradeModel { Name = validName };

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
        public async Task ApplySteelGradeNameRules_WhenEmptyOrNull_ShouldHaveNameRequiredErrorCode(string? emptyName)
        {
            // Arrange
            var model = new TestSteelGradeModel { Name = emptyName };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Name));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Name)
                  .WithErrorCode(ErrorCodes.NameRequired);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplySteelGradeNameRules_WhenExceeds100Characters_ShouldHaveInvalidSteelGradeNameErrorCode()
        {
            // Arrange 
            var model = new TestSteelGradeModel { Name = new string('S', 101) };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Name));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Name)
                  .WithErrorCode(ErrorCodes.InvalidSteelGradeName);
            await Task.CompletedTask;
        }

        // ─── ApplySteelGradeStandardRules ────────────────────────────────────

        [Test]
        [Arguments("EN 10088-3")]
        [Arguments("PN-EN 10025")]
        [Arguments(null)]
        [Arguments("")]
        public async Task ApplySteelGradeStandardRules_WhenValidLengthOrNull_ShouldNotHaveValidationError(string? validStandard)
        {
            // Arrange
            var model = new TestSteelGradeModel { Standard = validStandard };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Standard));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Standard);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplySteelGradeStandardRules_WhenExceeds50Characters_ShouldHaveInvalidSteelGradeStandardErrorCode()
        {
            // Arrange 
            var model = new TestSteelGradeModel { Standard = new string('N', 51) };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Standard));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Standard)
                  .WithErrorCode(ErrorCodes.InvalidSteelGradeStandard);
            await Task.CompletedTask;
        }

        // ─── ApplySteelGradeDensityRules ─────────────────────────────────────

        [Test]
        [Arguments(7900.0)]
        [Arguments(7850.5)]
        [Arguments(1.0)]
        public async Task ApplySteelGradeDensityRules_WhenGreaterThanZero_ShouldNotHaveValidationError(double validDensity)
        {
            // Arrange
            var model = new TestSteelGradeModel
            {
                Density = (decimal)validDensity,
                NullableDensity = (decimal)validDensity
            };

            // Act
            var result = _validator.TestValidate(model, opt =>
            {
                opt.IncludeProperties(x => x.Density);
                opt.IncludeProperties(x => x.NullableDensity);
            });

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Density);
            result.ShouldNotHaveValidationErrorFor(x => x.NullableDensity);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplySteelGradeDensityRules_WhenNullableDensityIsNull_ShouldNotHaveValidationError()
        {
            // Arrange
            var model = new TestSteelGradeModel { NullableDensity = null };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.NullableDensity));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.NullableDensity);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments(0.0)]
        [Arguments(-1.0)]
        [Arguments(-7900.0)]
        public async Task ApplySteelGradeDensityRules_WhenZeroOrLess_ShouldHaveInvalidSteelGradeDensityErrorCode(double invalidDensity)
        {
            // Arrange
            var model = new TestSteelGradeModel
            {
                Density = (decimal)invalidDensity,
                NullableDensity = (decimal)invalidDensity
            };

            // Act
            var result = _validator.TestValidate(model, opt =>
            {
                opt.IncludeProperties(x => x.Density);
                opt.IncludeProperties(x => x.NullableDensity);
            });

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Density)
                  .WithErrorCode(ErrorCodes.InvalidSteelGradeDensity);
            result.ShouldHaveValidationErrorFor(x => x.NullableDensity)
                  .WithErrorCode(ErrorCodes.InvalidSteelGradeDensity);
            await Task.CompletedTask;
        }
    }
}
