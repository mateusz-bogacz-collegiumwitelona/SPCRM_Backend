using Api.Request.Deal;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Deal
{
    public class AddDealProductValidator : AbstractValidator<AddDealProductRequest>
    {
        public AddDealProductValidator()
        {
            RuleFor(x => x.ProductId)
                 .ApplyValidGuidRule();

            RuleFor(x => x.Quantity)
                .ApplyDealProductQuantityRules();

            RuleFor(x => x.UnitPrice)
                .NotNull()
                .ApplyDealProductUnitPriceRules();
        }
    }
}
