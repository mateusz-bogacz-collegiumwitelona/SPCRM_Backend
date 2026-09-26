using Api.Request.Contact;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Contact
{
    public class EditContactDetailValidator : AbstractValidator<EditContactDetailRequest>
    {
        public EditContactDetailValidator()
        {
            When(x => x.Type != null, () =>
            {
                RuleFor(x => x.Type).ApplyTypeRules();
            });

            When(x => x.Label != null, () =>
            {
                RuleFor(x => x.Label).ApplyLabelRules();
            });

            When(x => x.Type != null && string.Equals(x.Type, "EMAIL", StringComparison.OrdinalIgnoreCase) && x.Value != null, () =>
            {
                RuleFor(x => x.Value).ApplyEmailRules();
            });

            When(x => ContactValidationRules.IsPhoneType(x.Type) && x.Value != null, () =>
            {
                RuleFor(x => x.Value).ApplyPhoneRules();
            });

            When(x => ContactValidationRules.IsFaxType(x.Type) && x.Value != null, () =>
            {
                RuleFor(x => x.Value).ApplyFaxRules();
            });

            When(x => x.Type != null && string.Equals(x.Type, "LINKEDIN", StringComparison.OrdinalIgnoreCase) && x.Value != null, () =>
            {
                RuleFor(x => x.Value).ApplyLinkedInRules();
            });
        }
    }
}
