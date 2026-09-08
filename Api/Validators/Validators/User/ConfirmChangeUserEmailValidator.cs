using Api.Request.User;
using Api.Validators.Rule;
using Domain.Constants;
using FluentValidation;

namespace Api.Validators.Validators.User
{
    public class ConfirmChangeUserEmailValidator : AbstractValidator<ConfirmChangeUserEmailRequest>
    {
        public ConfirmChangeUserEmailValidator()
        {
            RuleFor(x => x.UserId).ApplyValidGuidRule();
            RuleFor(x => x.Token)
                .NotEmpty()
                .WithErrorCode(ErrorCodes.TokenInvalid);
        }
    }
}
