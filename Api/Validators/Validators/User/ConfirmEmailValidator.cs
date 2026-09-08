using Api.Request.User;
using Api.Validators.Rule;
using Domain.Constants;
using FluentValidation;

namespace Api.Validators.Validators.User
{
    public class ConfirmEmailValidator : AbstractValidator<ConfirmEmailRequest>
    {
        public ConfirmEmailValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty()
                .ApplyUserEmailRules();

            RuleFor(x => x.Token)
                .NotEmpty()
                .WithErrorCode(ErrorCodes.TokenRequired);
        }
    }
}
