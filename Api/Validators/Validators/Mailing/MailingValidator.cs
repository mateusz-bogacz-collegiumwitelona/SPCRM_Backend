using Api.Request.Mailing;
using Api.Validators.Rule;
using Domain.Constants;
using FluentValidation;

namespace Api.Validators.Validators.Mailing
{
    public class MailingValidator : AbstractValidator<MailingRequest>
    {
        public MailingValidator()
        {
            RuleFor(x => x.To)
                .NotEmpty().WithErrorCode(ErrorCodes.ValidationError);

            RuleForEach(x => x.To)
                .ApplyValidGuidRule();

            RuleFor(x => x.Products)
                .NotEmpty().WithErrorCode(ErrorCodes.ValidationError);

            RuleForEach(x => x.Products)
                .SetValidator(new MailingProductValidator());

            RuleFor(x => x.Language)
                .NotEmpty().WithErrorCode(ErrorCodes.InvalidOperation)
                .ApplyLanguageRules();
        }
    }
}
