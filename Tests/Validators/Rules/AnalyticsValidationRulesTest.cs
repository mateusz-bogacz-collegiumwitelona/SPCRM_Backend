using Api.Validators.Rule;
using Domain.Constants;
using Domain.Enum;
using FluentValidation;
using FluentValidation.TestHelper;

namespace Tests.Validators.Rules
{
    public class AnalyticsValidationRulesTest
    {
        private class TestModel
        {
            public string Period { get; set; } = string.Empty;
        }

        private class TestModelValidator : AbstractValidator<TestModel>
        {
            public TestModelValidator()
            {
                RuleFor(x => x.Period).ApplyPeriodRules();
            }
        }

        private readonly TestModelValidator _validator = new();

        // ─── ApplyPeriodRules ─────────────────────────────

        [Test]
        public async Task ApplyPeriodRules_WhenValidEnumNamesProvided_ShouldNotHaveValidationErrors()
        {
            // Arrange
            var validNames = Enum.GetNames<AnalyticsPeriodEnum>();

            foreach (var name in validNames)
            {
                var modelExact = new TestModel { Period = name };
                var modelLower = new TestModel { Period = name.ToLower() };
                var modelUpper = new TestModel { Period = name.ToUpper() };

                // Act
                var resultExact = _validator.TestValidate(modelExact);
                var resultLower = _validator.TestValidate(modelLower);
                var resultUpper = _validator.TestValidate(modelUpper);

                // Assert
                resultExact.ShouldNotHaveValidationErrorFor(x => x.Period);
                resultLower.ShouldNotHaveValidationErrorFor(x => x.Period);
                resultUpper.ShouldNotHaveValidationErrorFor(x => x.Period);
            }

            await Task.CompletedTask;
        }

        [Test]
        [Arguments("niepoprawny_okres")]
        [Arguments("InvalidPeriod")]
        [Arguments("12345")]
        [Arguments("")]
        [Arguments("   ")]
        public async Task ApplyPeriodRules_WhenInvalidPeriodName_ShouldHaveValidationErrorWithCorrectErrorCode(string invalidPeriod)
        {
            // Arrange
            var model = new TestModel { Period = invalidPeriod };

            // Act
            var result = _validator.TestValidate(model);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Period)
                  .WithErrorCode(ErrorCodes.InvalidOperation);

            await Task.CompletedTask;
        }
    }
}
