using Api.Request.Auth;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Auth
{
    public class ForgotPasswordValidator : AbstractValidator<ForgotPasswordRequest>
    {
        public ForgotPasswordValidator()
        {
            RuleFor(x => x.Email).ApplyUserEmailRules();
        }
    }
}
