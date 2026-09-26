using Api.Request.Product.Contract;
using Api.Request.Promotion.Contract;
using Domain.Constants;
using Domain.Enum;
using FluentValidation;
using System.Numerics;

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

        public static IRuleBuilderOptions<T, string?> ApplyProductCategoryRule<T>(this IRuleBuilder<T, string?> ruleBuilder)
            => ruleBuilder
                .NotEmpty().WithErrorCode(ErrorCodes.InvalidCategory)
                .IsEnumName(typeof(ProductCategoryEnum), caseSensitive: false)
                .WithErrorCode(ErrorCodes.InvalidCategory);

        public static IRuleBuilderOptions<T, TProperty> ApplyProductDimmensionRule<T, TProperty>(this IRuleBuilder<T, TProperty> ruleBuilder)
            where TProperty : struct, INumber<TProperty>
            => ruleBuilder
                .GreaterThan(TProperty.Zero)
                .WithErrorCode(ErrorCodes.InvalidProductDimmension);

        public static IRuleBuilderOptions<T, TProperty?> ApplyProductDimmensionRule<T, TProperty>(this IRuleBuilder<T, TProperty?> ruleBuilder)
            where TProperty : struct, INumber<TProperty>
            => ruleBuilder
                .GreaterThan(TProperty.Zero)
                .WithErrorCode(ErrorCodes.InvalidProductDimmension);

        public static IRuleBuilderOptions<T, TProperty> ApplyProductWeightRule<T, TProperty>(this IRuleBuilder<T, TProperty> ruleBuilder)
            where TProperty : struct, INumber<TProperty>
            => ruleBuilder
                .GreaterThan(TProperty.Zero)
                .WithErrorCode(ErrorCodes.InvalidProductWeight);

        public static IRuleBuilderOptions<T, TProperty?> ApplyProductWeightRule<T, TProperty>(this IRuleBuilder<T, TProperty?> ruleBuilder)
            where TProperty : struct, INumber<TProperty>
            => ruleBuilder
                .GreaterThan(TProperty.Zero)
                .WithErrorCode(ErrorCodes.InvalidProductWeight);

        public static IRuleBuilderOptions<T, TProperty> ApplyProductPricePerUnitRule<T, TProperty>(this IRuleBuilder<T, TProperty> ruleBuilder)
            where TProperty : struct, INumber<TProperty>
            => ruleBuilder
                .GreaterThan(TProperty.Zero)
                .WithErrorCode(ErrorCodes.InvalidProductPricePerUnit);

        public static IRuleBuilderOptions<T, TProperty?> ApplyProductPricePerUnitRule<T, TProperty>(this IRuleBuilder<T, TProperty?> ruleBuilder)
            where TProperty : struct, INumber<TProperty>
            => ruleBuilder
                .GreaterThan(TProperty.Zero)
                .WithErrorCode(ErrorCodes.InvalidProductPricePerUnit);

        public static IRuleBuilderOptions<T, TProperty> ApplyProductStockQuantityRule<T, TProperty>(this IRuleBuilder<T, TProperty> ruleBuilder)
            where TProperty : struct, INumber<TProperty>
            => ruleBuilder
                .GreaterThanOrEqualTo(TProperty.Zero)
                .WithErrorCode(ErrorCodes.InvalidProductStockQuantity);

        public static IRuleBuilderOptions<T, TProperty?> ApplyProductStockQuantityRule<T, TProperty>(this IRuleBuilder<T, TProperty?> ruleBuilder)
            where TProperty : struct, INumber<TProperty>
            => ruleBuilder
                .GreaterThan(TProperty.Zero)
                .WithErrorCode(ErrorCodes.InvalidProductStockQuantity);

        public static IRuleBuilderOptions<T, T> ApplyAddPromotionDiscountExclusiveRule<T>(this IRuleBuilder<T, T> ruleBuilder)
          where T : IPromotionDiscountContract
              => ruleBuilder
                  .Must(x => (x.DiscountPercentage.HasValue && !x.PromotionalPrice.HasValue)
                          || (!x.DiscountPercentage.HasValue && x.PromotionalPrice.HasValue))
                  .WithErrorCode(ErrorCodes.DiscountPercentageAndPriceCannotBothChoice);

        public static IRuleBuilderOptions<T, T> ApplyEditPromotionDiscountExclusiveRule<T>(this IRuleBuilder<T, T> ruleBuilder)
            where T : IPromotionDiscountContract
                => ruleBuilder
                    .Must(x => !(x.DiscountPercentage.HasValue && x.PromotionalPrice.HasValue))
                    .WithErrorCode(ErrorCodes.DiscountPercentageAndPriceCannotBothChoice);

        public static void ApplyProductCategoryDimensionsRules<T>(this AbstractValidator<T> validator)
            where T : IAddProductDimensionsContract
        {
            validator.RuleFor(x => x.Diameter)
                .NotNull()
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.InvalidProductDimmension)
                .When(x => string.Equals(x.Category, ProductCategoryEnum.Pipe.ToString(), StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(x.Category, ProductCategoryEnum.Wire.ToString(), StringComparison.OrdinalIgnoreCase));

            validator.RuleFor(x => x)
                .Must(x => (x.Diameter.HasValue && x.Diameter.Value > 0) || (x.Width > 0 && x.Thickness > 0))
                .WithErrorCode(ErrorCodes.DiameterIsRequiredForPipeAndWire)
                .When(x => string.Equals(x.Category, ProductCategoryEnum.Bar.ToString(), StringComparison.OrdinalIgnoreCase));
        }

        public static void ApplyEditProductCategoryDimensionsRules<T>(this AbstractValidator<T> validator)
            where T : IEditProductDimensionsContract
        {
            validator.RuleFor(x => x.Diameter)
                .NotNull()
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.DiameterIsRequiredForPipeAndWire)
                .When(x => string.Equals(x.Category, ProductCategoryEnum.Pipe.ToString(), StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(x.Category, ProductCategoryEnum.Wire.ToString(), StringComparison.OrdinalIgnoreCase));

            validator.RuleFor(x => x)
                .Must(x => (x.Diameter.HasValue && x.Diameter.Value > 0) ||
                           (x.Width.HasValue && x.Width.Value > 0 && x.Thickness.HasValue && x.Thickness.Value > 0))
                .WithErrorCode(ErrorCodes.DiameterIsRequiredForPipeAndWire)
                .When(x => string.Equals(x.Category, ProductCategoryEnum.Bar.ToString(), StringComparison.OrdinalIgnoreCase));
        }
    }
}
