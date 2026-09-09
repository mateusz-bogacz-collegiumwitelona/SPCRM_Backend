using Api.Request.Contact;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Contact
{
    public class ContactFilterValidator : AbstractValidator<ContactFilterRequest>
    {
        public ContactFilterValidator()
        {
            RuleFor(x => x.OwnerId).ApplyValidGuidRule();
        }
    }
}
