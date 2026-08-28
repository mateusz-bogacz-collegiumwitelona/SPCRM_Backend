using Api.Request.Contact;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Contact
{
    public class EditContactValidator : AbstractValidator<EditContactRequest>
    {
        public EditContactValidator()
        {
            RuleFor(x => x.ContactId).ApplyValidGuidRule();

            When(x => !string.IsNullOrEmpty(x.FirstName), () =>
            {
                RuleFor(x => x.FirstName).ApplyNameRules();
            });

            When(x => !string.IsNullOrEmpty(x.LastName), () =>
            {
                RuleFor(x => x.LastName).ApplyNameRules();
            });

            When(x => x.Details != null, () =>
            {
                RuleForEach(x => x.Details)
                    .SetValidator(new EditContactDetailValidator());
            });
        }
    }
}
