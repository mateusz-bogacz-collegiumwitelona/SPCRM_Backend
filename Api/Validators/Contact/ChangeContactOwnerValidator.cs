using Api.Request.Contact;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Contact
{
    public class ChangeContactOwnerValidator : AbstractValidator<ChangeContactOwnerRequest>
    {
        public ChangeContactOwnerValidator()
        {
            RuleFor(x => x.NewOwnerId).ApplyValidGuidRule();
            RuleFor(x => x.ContactId).ApplyValidGuidRule();
        }
    }
}
