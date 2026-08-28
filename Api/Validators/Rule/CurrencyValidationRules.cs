using Domain.Constants;
using FluentValidation;

namespace Api.Validators.Rule
{
    public static class CurrencyValidationRules
    {
        public static IRuleBuilderOptions<T, int> ApplyCurrencyDecimalPlacesRules<T>(this IRuleBuilder<T, int> ruleBuilder)
            => ruleBuilder
                .InclusiveBetween(0, 4)
                .WithErrorCode(ErrorCodes.DecimalPlacesInvalid);

        public static IRuleBuilderOptions<T, int?> ApplyCurrencyDecimalPlacesRules<T>(this IRuleBuilder<T, int?> ruleBuilder)
            => ruleBuilder
                .InclusiveBetween(0, 4)
                .WithErrorCode(ErrorCodes.DecimalPlacesInvalid);

        public static IRuleBuilderOptions<T, string?> ApplyCurrencyCodeRules<T>(this IRuleBuilder<T, string?> ruleBuilder)
            => ruleBuilder
                .NotEmpty()
                .WithErrorCode(ErrorCodes.CodeRequired)
                .Matches("^[a-zA-Z]{3}$")
                .WithErrorCode(ErrorCodes.CodeFormatInvalid);

        public static IRuleBuilderOptions<T, string?> ApplyCurrencyNameRules<T>(this IRuleBuilder<T, string?> ruleBuilder)
            => ruleBuilder
                .NotEmpty()
                .WithErrorCode(ErrorCodes.NameRequired)
                .MaximumLength(100)
                .WithErrorCode(ErrorCodes.NameLengthInvalid);
    }
}
