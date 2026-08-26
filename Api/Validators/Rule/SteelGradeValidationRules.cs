using Domain.Constants;
using FluentValidation;

namespace Api.Validators.Rule
{
    public static class SteelGradeValidationRules
    {
        public static IRuleBuilderOptions<T, string?> ApplySteelGradeNameRules<T>(this IRuleBuilder<T, string?> ruleBuilder)
            => ruleBuilder
                .NotEmpty()
                .WithErrorCode(ErrorCodes.NameRequired)
                .MaximumLength(100)
                .WithErrorCode(ErrorCodes.InvalidSteelGradeName);

        public static IRuleBuilderOptions<T, string?> ApplySteelGradeStandardRules<T>(this IRuleBuilder<T, string?> ruleBuilder)
            => ruleBuilder
                .MaximumLength(50)
                .WithErrorCode(ErrorCodes.InvalidSteelGradeStandard);

        public static IRuleBuilderOptions<T, decimal> ApplySteelGradeDensityRules<T>(this IRuleBuilder<T, decimal> ruleBuilder)
            => ruleBuilder
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.InvalidSteelGradeDensity);

        public static IRuleBuilderOptions<T, decimal?> ApplySteelGradeDensityRules<T>(this IRuleBuilder<T, decimal?> ruleBuilder)
            => ruleBuilder
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.InvalidSteelGradeDensity);
    }
}
