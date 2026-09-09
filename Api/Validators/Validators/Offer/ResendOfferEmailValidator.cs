using Api.Request.Offer;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Offer
{
    public class ResendOfferEmailValidator : AbstractValidator<ResendOfferEmailRequest>
    {
        public ResendOfferEmailValidator()
        {
            RuleFor(x => x.OfferId)
                .ApplyValidGuidRule();

            RuleFor(x => x.Language).ApplyLanguageRules();
        }
    }
}
