using Api.Request.Auth;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Auth
{
    public class ResetPasswordValidator : AbstractValidator<ResetPasswordRequest>
    {
        public ResetPasswordValidator()
        {
            RuleFor(x => x.UserId).ApplyValidGuidRule();
            RuleFor(x => x.Token).ApplyValidTokenRule();
            RuleFor(x => x.Password).ApplyPasswordRules();
            RuleFor(x => x.ConfirmPassword).ApplyConfirmPasswordRules(x => x.Password);
        }
    }
}
