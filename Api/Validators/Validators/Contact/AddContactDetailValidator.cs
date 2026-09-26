using Api.Request.Contact;
using Api.Validators.Rule;
using Domain.Constants;
using FluentValidation;

namespace Api.Validators.Validators.Contact
{
    public class AddContactDetailValidator : AbstractValidator<AddContactDetailRequest>
    {
        public AddContactDetailValidator()
        {
            RuleFor(x => x.Type)
                .NotEmpty()
                .ApplyTypeRules();

            RuleFor(x => x.Label)
                .NotEmpty().WithErrorCode(ErrorCodes.LabelRequired)
                .ApplyLabelRules();

            RuleFor(x => x.Value)
                .NotEmpty().WithErrorCode(ErrorCodes.ContactValueRequired);

            When(x => string.Equals(x.Type, "EMAIL", StringComparison.OrdinalIgnoreCase), () =>
            {
                RuleFor(x => x.Value)
                    .NotEmpty().WithErrorCode(ErrorCodes.EmailRequired)
                    .ApplyEmailRules();
            });

            When(x => ContactValidationRules.IsPhoneType(x.Type), () =>
            {
                RuleFor(x => x.Value)
                    .NotEmpty().WithErrorCode(ErrorCodes.NumberRequired)
                    .ApplyPhoneRules();
            });

            When(x => ContactValidationRules.IsFaxType(x.Type), () =>
            {
                RuleFor(x => x.Value)
                    .NotEmpty().WithErrorCode(ErrorCodes.NumberRequired)
                    .ApplyFaxRules();
            });

            When(x => string.Equals(x.Type, "LINKEDIN", StringComparison.OrdinalIgnoreCase), () =>
            {
                RuleFor(x => x.Value)
                    .NotEmpty().WithErrorCode(ErrorCodes.LinkedInUrlRequired)
                    .ApplyLinkedInRules();
            });
        }
    }
}
