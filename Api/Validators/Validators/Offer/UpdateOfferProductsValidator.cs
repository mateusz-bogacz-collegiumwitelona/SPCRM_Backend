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
             .ApplyOfferIdRules();

            RuleFor(x => x.Items)
                .NotEmpty()
                .WithErrorCode(ErrorCodes.OfferProductsRequired);

            RuleForEach(x => x.Items)
                .SetValidator(new OfferProductItemValidator());
        }
    }
}
