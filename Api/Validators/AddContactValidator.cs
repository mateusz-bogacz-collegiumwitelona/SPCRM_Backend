using Api.Request;
using Api.Validators.Rule;
using Domain.Constants;
using FluentValidation;

namespace Api.Validators
{
    public class AddContactValidator : AbstractValidator<AddContactRequest>
    {
        public AddContactValidator() 
        {
            RuleFor(x => x.CompanyId).ApplyValidGuidRule();

            RuleFor(x => x.FirstName)
                .NotEmpty().WithErrorCode(ErrorCodes.NameRequired) 
                .Length(5, 100).WithErrorCode(ErrorCodes.NameLengthInvalid);

            RuleFor(x => x.LastName)
                .NotEmpty().WithErrorCode(ErrorCodes.NameRequired) 
                .Length(5, 100).WithErrorCode(ErrorCodes.NameLengthInvalid); 

            RuleFor(x => x.Details)
                .NotEmpty()
                .WithErrorCode(ErrorCodes.ValidationError);

            RuleFor(x => x.Details)
                .Must(details => details != null && details.Count(d => d.IsPrimary) == 1)
                .WithErrorCode(ErrorCodes.PrimaryContactDetailRequired);

            RuleForEach(x => x.Details)
                .SetValidator(new AddContactDetailCommandValidator());
        }
    }
}
