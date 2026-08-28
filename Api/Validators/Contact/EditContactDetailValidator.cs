using Api.Request.Contact;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Contact
{
    public class EditContactDetailValidator : AbstractValidator<EditContactDetailRequest>
    {
        public EditContactDetailValidator()
        {
            When(x => !string.IsNullOrEmpty(x.Type), () =>
            {
                RuleFor(x => x.Type).ApplyTypeRules();
            });

            When(x => !string.IsNullOrEmpty(x.Label), () =>
            {
                RuleFor(x => x.Label).ApplyLabelRules();
            });

            When(x => !string.IsNullOrEmpty(x.Type) && string.Equals(x.Type, "EMAIL", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(x.Value), () =>
            {
                RuleFor(x => x.Value).ApplyEmailRules();
            });

            When(x => ContactValidationRules.IsPhoneType(x.Type) && !string.IsNullOrEmpty(x.Value), () =>
            {
                RuleFor(x => x.Value).ApplyPhoneRules();
            });

            When(x => !string.IsNullOrEmpty(x.Type) && string.Equals(x.Type, "LINKEDIN", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(x.Value), () =>
            {
                RuleFor(x => x.Value).ApplyLinkedInRules();
            });
        }
    }
}
