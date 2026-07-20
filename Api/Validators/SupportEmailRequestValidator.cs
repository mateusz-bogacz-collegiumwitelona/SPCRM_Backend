using Domain.Constants;
using Api.Request;
using FluentValidation;

namespace Api.Validators
{
    public class SupportEmailRequestValidator : AbstractValidator<SupportEmailRequest>
    {
        public SupportEmailRequestValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithErrorCode(ErrorCodes.EmailRequired)
                .EmailAddress().WithErrorCode(ErrorCodes.EmailInvalid);

            RuleFor(x => x.Title)
                .NotEmpty().WithErrorCode(ErrorCodes.TitleRequired)
                .Length(5, 100).WithErrorCode(ErrorCodes.TitleLengthInvalid);

            RuleFor(x => x.Message)
                .NotEmpty().WithErrorCode(ErrorCodes.MessageRequired)
                .Length(5, 5000).WithErrorCode(ErrorCodes.MessageLengthInvalid);
        }
    }
}
