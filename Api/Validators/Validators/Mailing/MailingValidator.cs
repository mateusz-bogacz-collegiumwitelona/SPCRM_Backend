using Api.Request.Mailing;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Mailing
{
    public class MailingValidator : AbstractValidator<MailingRequest>
    {
        public MailingValidator() 
        { 
            RuleForEach(x => x.To).ApplyValidGuidRule();

            RuleForEach(x => x.Products).SetValidator(new MailingProductValidator());

            RuleFor(x => x.Language).ApplyLanguageRules();
        }
    }
}
