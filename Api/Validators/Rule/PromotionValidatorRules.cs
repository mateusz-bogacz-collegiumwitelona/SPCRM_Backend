using Domain.Constants;
using FluentValidation;

namespace Api.Validators.Rule
{
    public static class PromotionValidatorRules
    {
        public static IRuleBuilderOptions<T, Guid> ApplyPromotionIdRules<T>(this IRuleBuilder<T, Guid> ruleBuilder)
            => ruleBuilder.ApplyValidGuidRule();

        public static IRuleBuilderOptions<T, string?> ApplyPromotionNameRules<T>(this IRuleBuilder<T, string?> ruleBuilder)
            => ruleBuilder
                .NotEmpty()
                .MaximumLength(150)
                .WithErrorCode(ErrorCodes.InvalidPromotionName);

        public static IRuleBuilderOptions<T, decimal?> ApplyDiscountPercentageRule<T>(this IRuleBuilder<T, decimal?> ruleBuilder)
            => ruleBuilder
                .InclusiveBetween(1, 100)
                .WithErrorCode(ErrorCodes.InvalidPromotionDiscount);

        public static IRuleBuilderOptions<T, long?> ApplyPromotionalPriceRule<T>(this IRuleBuilder<T, long?> ruleBuilder)
            => ruleBuilder
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.InvalidPromotionDiscount);

        public static IRuleBuilderOptions<T, Guid?> ApplyOptionalGuidRule<T>(this IRuleBuilder<T, Guid?> ruleBuilder)
            => ruleBuilder
                .Must(id => !id.HasValue || id.Value != Guid.Empty)
                .WithErrorCode(ErrorCodes.GuidInvalid);

        public static IRuleBuilderOptions<T, int?> ApplyMinQuantityRules<T>(this IRuleBuilder<T, int?> ruleBuilder)
            => ruleBuilder
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.InvalidPromotioMinQuantity);

        public static IRuleBuilderOptions<T, int?> ApplyMinWeightRules<T>(this IRuleBuilder<T, int?> ruleBuilder)
            => ruleBuilder
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.InvalidPromotioMinWeight);
    }
}
