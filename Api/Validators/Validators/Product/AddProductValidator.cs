using Api.Request.Product;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Product
{
    public class AddProductValidator : AbstractValidator<AddProductRequest>
    {
        public AddProductValidator()
        {
            RuleFor(x => x.Name)
                .ApplyProductNameRules();

            RuleFor(x => x.SteelGradeId)
                .ApplyValidGuidRule();

            RuleFor(x => x.Thickness)
                .ApplyProductDimmensionRule();

            RuleFor(x => x.Weight)
                .ApplyProductWeightRule();

            RuleFor(x => x.UnitId)
                .ApplyValidGuidRule();

            RuleFor(x => x.PricePerUnit)
                .ApplyProductPricePerUnitRule();

            RuleFor(x => x.StockQuantity)
                .ApplyProductStockQuantityRule();

            RuleFor(x => x.Category)
                .ApplyProductCategoryRule();

            RuleFor(x => x.Width)
                .ApplyProductDimmensionRule();

            RuleFor(x => x.Length)
                .ApplyProductDimmensionRule();

            RuleFor(x => x.CurrencyId)
                .ApplyValidGuidRule();

            this.ApplyProductCategoryDimensionsRules();
        }
    }
}
