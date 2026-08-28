using Api.Request.Support;
using Domain.Constants;
using FluentValidation;

namespace Api.Validators.Support
{
    public class SupportEmailValidator : AbstractValidator<SupportEmailRequest>
    {
        public SupportEmailValidator()
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
