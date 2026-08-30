using Domain.Constants;
using FluentValidation;

namespace Api.Validators.Rule
{
    public static class UnitValidationRules
    {
        public static IRuleBuilderOptions<T, string?> ApplyUnitNameRules<T>(this IRuleBuilder<T, string?> ruleBuilder)
            => ruleBuilder
                .NotEmpty()
                .WithErrorCode(ErrorCodes.NameRequired)
                .MaximumLength(100)
                .WithErrorCode(ErrorCodes.InvalidUnitName);

        public static IRuleBuilderOptions<T, string?> ApplyUnitSymbolRules<T>(this IRuleBuilder<T, string?> ruleBuilder)
            => ruleBuilder
                .NotEmpty()
                .WithErrorCode(ErrorCodes.InvalidUnitSymbol);

        public static IRuleBuilderOptions<T, int> ApplyUnitBaseMultiplierRules<T>(this IRuleBuilder<T, int> ruleBuilder)
           => ruleBuilder
               .GreaterThan(0)
               .WithErrorCode(ErrorCodes.InvalidUnitBaseMultiplier);

        public static IRuleBuilderOptions<T, int?> ApplyUnitBaseMultiplierRules<T>(this IRuleBuilder<T, int?> ruleBuilder)
            => ruleBuilder
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.InvalidUnitBaseMultiplier);
    }
}
