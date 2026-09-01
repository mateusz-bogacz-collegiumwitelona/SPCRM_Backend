using Api.Request.Offer;
using Api.Validators.Rule;
using Domain.Constants;
using FluentValidation;

namespace Api.Validators.Validators.Offer
{
    public class UpdateOfferProductsValidator : AbstractValidator<UpdateOfferProductsRequest>
    {
        public UpdateOfferProductsValidator()
        {
            RuleFor(x => x.OfferId)
                .ApplyValidGuidRule();

            RuleFor(x => x.Items)
                .NotEmpty()
                .WithMessage("Offer must contain at least one product.")
                .WithErrorCode(ErrorCodes.InvalidOperation);

            RuleForEach(x => x.Items).ChildRules(item =>
            {
                item.RuleFor(i => i.ProductId)
                    .ApplyValidGuidRule();

                item.RuleFor(i => i.Quantity)
                    .GreaterThan(0)
                    .WithErrorCode(ErrorCodes.InvalidOperation);

                item.RuleFor(i => i.QuotedPrice)
                    .GreaterThan(0)
                    .WithErrorCode(ErrorCodes.InvalidOperation);
            });
        }
    }
}
