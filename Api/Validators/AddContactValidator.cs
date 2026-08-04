using Api.Request;
using Domain.Constants;
using FluentValidation;

namespace Api.Validators
{
    public class AddContactValidator : AbstractValidator<AddContactRequest>
    {
        public AddContactValidator() 
        {
            RuleFor(x => x.CompanyId)
                .NotEmpty()
                .NotEqual(Guid.Empty)
                .WithErrorCode(ErrorCodes.ValidationError);

            RuleFor(x => x.FirstName)
                .NotEmpty()
                .WithErrorCode(ErrorCodes.ValidationError);

            RuleFor(x => x.LastName)
                .NotEmpty()
                .WithErrorCode(ErrorCodes.ValidationError);

            RuleFor(x => x.Details)
                .NotEmpty()
                .WithErrorCode(ErrorCodes.ValidationError);

            RuleFor(x => x.Details)
                .Must(details => details != null && details.Count(d => d.IsPrimary) == 1)
                .WithErrorCode(ErrorCodes.ValidationError);

            RuleForEach(x => x.Details)
                .SetValidator(new AddContactDetailCommandValidator());
        }
    }
}
