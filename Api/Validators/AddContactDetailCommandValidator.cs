using Api.Request;
using Api.Validators.Rule;
using Domain.Constants;
using FluentValidation;

namespace Api.Validators
{
    public class AddContactDetailCommandValidator : AbstractValidator<AddContactDetailRequest>
    {
        public AddContactDetailCommandValidator()
        {
            RuleFor(x => x.Type)
                .NotEmpty()
                .ApplyTypeRules();

            RuleFor(x => x.Label)
                .NotEmpty()
                .WithErrorCode(ErrorCodes.LabelRequired)
                .ApplyLabelRules();

            When(x => string.Equals(x.Type, "EMAIL", StringComparison.OrdinalIgnoreCase), () =>
            {
                RuleFor(x => x.Value)
                    .NotEmpty()
                    .WithErrorCode(ErrorCodes.EmailRequired)
                    .ApplyEmailRules();
            });

            When(x => ContactValidationRules.IsPhoneType(x.Type), () =>
            {
                RuleFor(x => x.Value)
                    .NotEmpty()
                    .WithErrorCode(ErrorCodes.NumberRequired)
                    .ApplyPhoneRules();
            });

            When(x => string.Equals(x.Type, "LINKEDIN", StringComparison.OrdinalIgnoreCase), () =>
            {
                RuleFor(x => x.Value)
                    .NotEmpty()
                    .WithErrorCode(ErrorCodes.LinkedInUrlRequired)
                    .ApplyLinkedInRules();
            });
        }
    }
}
