using Domain.Constants;
using FluentValidation;

namespace Api.Validators.Rule
{
    public static class ProductValidationRules
    {
        public static IRuleBuilderOptions<T, Guid> ApplyProductIdRules<T>(this IRuleBuilder<T, Guid> ruleBuilder)
            => ruleBuilder.ApplyValidGuidRule();

        public static IRuleBuilderOptions<T, string?> ApplyProductNameRules<T>(this IRuleBuilder<T, string?> ruleBuilder)
            => ruleBuilder
                .NotEmpty()
                .MaximumLength(150)
                .WithErrorCode(ErrorCodes.InvalidProductName);

        public static IRuleBuilderOptions<T, string?> ApplyProductSteelGradeRules<T>(this IRuleBuilder<T, string?> ruleBuilder)
            => ruleBuilder
                .NotEmpty()
                .MaximumLength(50)
                .WithErrorCode(ErrorCodes.InvalidProductSteelGrade);

        // Wersje dla int (nullable i non-nullable)
        public static IRuleBuilderOptions<T, int?> ApplyProductDimmensionRule<T>(this IRuleBuilder<T, int?> ruleBuilder)
            => ruleBuilder
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.InvalidProductDimmension);

        public static IRuleBuilderOptions<T, int> ApplyProductDimmensionRule<T>(this IRuleBuilder<T, int> ruleBuilder)
            => ruleBuilder
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.InvalidProductDimmension);

        public static IRuleBuilderOptions<T, decimal?> ApplyProductWeightRule<T>(this IRuleBuilder<T, decimal?> ruleBuilder)
            => ruleBuilder
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.InvalidProductWeight);

        public static IRuleBuilderOptions<T, decimal> ApplyProductWeightRule<T>(this IRuleBuilder<T, decimal> ruleBuilder)
            => ruleBuilder
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.InvalidProductWeight);

        public static IRuleBuilderOptions<T, decimal?> ApplyProductPricePerUnitRule<T>(this IRuleBuilder<T, decimal?> ruleBuilder)
            => ruleBuilder
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.InvalidProductPricePerUnit);

        public static IRuleBuilderOptions<T, decimal> ApplyProductPricePerUnitRule<T>(this IRuleBuilder<T, decimal> ruleBuilder)
            => ruleBuilder
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.InvalidProductPricePerUnit);

        public static IRuleBuilderOptions<T, int?> ApplyProductStockQuantityRule<T>(this IRuleBuilder<T, int?> ruleBuilder)
            => ruleBuilder
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.InvalidProductStockQuantity);

        public static IRuleBuilderOptions<T, string?> ApplyProductCategoryRule<T>(this IRuleBuilder<T, string?> ruleBuilder)
            => ruleBuilder
                .NotEmpty()
                .WithErrorCode(ErrorCodes.InvalidCategory);
    }
}
