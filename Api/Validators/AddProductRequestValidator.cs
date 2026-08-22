using Api.Request;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators
{
    public class AddProductValidator : AbstractValidator<AddProductRequest>
    {
        public AddProductValidator()
        {
            RuleFor(x => x.Name)
                .ApplyProductNameRules();

            RuleFor(x => x.SteelGrade)
                .ApplyProductSteelGradeRules();

            RuleFor(x => x.Thickness)
                .NotNull()
                .ApplyProductDimmensionRule();

            RuleFor(x => x.Weight)
                .NotNull()
                .ApplyProductWeightRule();

            RuleFor(x => x.UnitId)
                .ApplyValidGuidRule();

            RuleFor(x => x.PricePerUnit)
                .NotNull()
                .ApplyProductPricePerUnitRule();

            RuleFor(x => x.Category)
                .ApplyProductCategoryRule();
        }
    }
}
