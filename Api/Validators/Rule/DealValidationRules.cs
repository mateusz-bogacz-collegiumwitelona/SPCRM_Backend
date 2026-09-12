using Domain.Constants;
using Domain.Enum;
using FluentValidation;

namespace Api.Validators.Rule
{
    public static class DealValidationRules
    {

        public static IRuleBuilderOptions<T, DateTime> ApplyDealCloseDateRules<T>(this IRuleBuilder<T, DateTime> ruleBuilder)
            => ruleBuilder
                .NotEmpty().WithErrorCode(ErrorCodes.InvalidDate)
                .GreaterThan(DateTime.UtcNow.AddMinutes(-5)).WithErrorCode(ErrorCodes.InvalidDate);

        public static IRuleBuilderOptions<T, int> ApplyDealProductQuantityRules<T>(this IRuleBuilder<T, int> ruleBuilder)
            => ruleBuilder
                .GreaterThan(0).WithErrorCode(ErrorCodes.DealQuantityInvalid);

        public static IRuleBuilderOptions<T, int?> ApplyDealProductQuantityRules<T>(this IRuleBuilder<T, int?> ruleBuilder)
            => ruleBuilder
                .GreaterThan(0).WithErrorCode(ErrorCodes.DealQuantityInvalid);

        public static IRuleBuilderOptions<T, long> ApplyDealProductUnitPriceRules<T>(this IRuleBuilderOptions<T, long> ruleBuilder)
            => ruleBuilder
                .GreaterThanOrEqualTo(0)
                .WithErrorCode(ErrorCodes.DealProductUnitPriceInvalid);

        public static IRuleBuilderOptions<T, string> ApplyDealStatusRules<T>(this IRuleBuilder<T, string> ruleBuilder)
            => ruleBuilder
                .NotEmpty().WithErrorCode(ErrorCodes.InvalidOperation)
                .IsEnumName(typeof(DealsStatusEnum), caseSensitive: false)
                .WithErrorCode(ErrorCodes.InvalidOperation);
    }
}
