using Api.Request.Offer;
using Api.Validators.Rule;
using Domain.Constants;
using FluentValidation;

namespace Api.Validators.Validators.Offer
{
    public class OfferProductItemValidator : AbstractValidator<OfferProductItemRequest>
    {
        public OfferProductItemValidator()
        {
            RuleFor(x => x.ProductId).ApplyValidGuidRule();

            RuleFor(x => x.Quantity)
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.OfferQuantityInvalid);

            RuleFor(x => x.QuotedPrice)
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.OfferQuotedPriceInvalid);
        }
    }
}
