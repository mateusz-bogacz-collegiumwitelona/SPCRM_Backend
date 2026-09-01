using Api.Request.Offer;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Offer
{
    public class ChangeOfferStatusValidator : AbstractValidator<ChangeOfferStatusRequest>
    {
        public ChangeOfferStatusValidator() 
        { 
            RuleFor(x => x.OfferId).ApplyOfferIdRules();
            RuleFor(x => x.NewStatus).ApplyOfferStatusRules();
        }
    }
}
