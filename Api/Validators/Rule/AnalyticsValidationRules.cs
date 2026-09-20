using Domain.Constants;
using Domain.Enum;
using FluentValidation;

namespace Api.Validators.Rule
{
    public static class AnalyticsValidationRules
    {
        public static IRuleBuilderOptions<T, string> ApplyPeriodRules<T>(this IRuleBuilder<T, string> ruleBuilder)
            => ruleBuilder
                .IsEnumName(typeof(AnalyticsPeriodEnum), caseSensitive: false)
                .WithErrorCode(ErrorCodes.InvalidOperation);

    }
}
