using Api.Request.Offer;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Offer
{
    public class ExtendOfferValidityValidator : AbstractValidator<ExtendOfferValidityRequest>
    {
        public ExtendOfferValidityValidator()
        {
            RuleFor(x => x.OfferId)
                .ApplyOfferIdRules();

            RuleFor(x => x.NewValidUntil)
                .ApplyNewValidUntilRules();
        }
    }
}
