using Api.Request.Product;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Product
{
    public class EditProductValidator : AbstractValidator<EditProductRequest>
    {
        public EditProductValidator()
        {
            RuleFor(x => x.ProductId)
            .ApplyProductIdRules();

            RuleFor(x => x.Name)
                .ApplyProductNameRules()
                .When(x => !string.IsNullOrWhiteSpace(x.Name));

            RuleFor(x => x.SteelGradeId)
                .ApplyValidGuidRule()
                .When(x => x.SteelGradeId.HasValue);

            RuleFor(x => x.UnitId)
                .ApplyValidGuidRule()
                .When(x => x.UnitId.HasValue);

            RuleFor(x => x.CurrencyId)
                .ApplyValidGuidRule()
                .When(x => x.CurrencyId.HasValue);

            RuleFor(x => x.Thickness)
                .ApplyProductDimmensionRule();

            RuleFor(x => x.Width)
                .ApplyProductDimmensionRule();

            RuleFor(x => x.Length)
                .ApplyProductDimmensionRule();

            RuleFor(x => x.Diameter)
                .ApplyProductDimmensionRule();

            RuleFor(x => x.Weight)
                .ApplyProductWeightRule();

            RuleFor(x => x.PricePerUnit)
                .ApplyProductPricePerUnitRule();

            RuleFor(x => x.StockQuantity)
                .ApplyProductStockQuantityRule();

            RuleFor(x => x.Category)
                .ApplyProductCategoryRule()
                .When(x => !string.IsNullOrWhiteSpace(x.Category));

            this.ApplyEditProductCategoryDimensionsRules();
        }
    }
}
