using Api.Request.Offer;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Offer
{
    public class OfferListValidator : AbstractValidator<OfferListRequest>
    {
        public OfferListValidator()
        {
            RuleFor(x => x.PageNumber).ApplyPageNumberRules();
            RuleFor(x => x.PageSize).ApplyPageSizeRules();
        }
    }
}
