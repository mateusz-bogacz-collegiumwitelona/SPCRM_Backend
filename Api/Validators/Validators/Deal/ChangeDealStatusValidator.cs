using Api.Request.Deal;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Deal
{
    public class ChangeDealStatusValidator : AbstractValidator<ChangeDealStatusRequest>
    {
        public ChangeDealStatusValidator()
        {
            RuleFor(x => x.TargetStatus).ApplyDealStatusRules();

            RuleFor(x => x.Language).ApplyLanguageRules();

            RuleFor(x => x.CustomRecipientEmail).ApplyEmailRules();
        }
    }
}
