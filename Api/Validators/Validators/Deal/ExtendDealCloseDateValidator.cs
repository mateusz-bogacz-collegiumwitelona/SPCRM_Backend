using Api.Request.Deal;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Deal
{
    public class ExtendDealCloseDateValidator : AbstractValidator<ExtendDealCloseDateRequest>
    {
        public ExtendDealCloseDateValidator()
        {
            RuleFor(x => x.DealId).ApplyValidGuidRule();

            RuleFor(x => x.NewCloseDate).ApplyDealCloseDateRules();
        }
    }
}
