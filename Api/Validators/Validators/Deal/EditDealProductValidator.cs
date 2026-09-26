using Api.Request.Deal;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Deal
{
    public class EditDealProductValidator : AbstractValidator<EditDealProductRequest>
    {
        public EditDealProductValidator()
        {
            RuleFor(x => x.DealProductId).ApplyValidGuidRule();

            RuleFor(x => x.Quantity)
                .ApplyDealProductQuantityRules();

            RuleFor(x => x.UnitPrice)
                .ApplyDealProductUnitPriceRules()
                .When(x => x.UnitPrice.HasValue);
        }
    }
}
