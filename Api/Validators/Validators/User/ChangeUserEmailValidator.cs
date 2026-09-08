using Api.Request.User;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.User
{
    public class ChangeUserEmailValidator : AbstractValidator<ChangeUserEmailRequest>
    {
        public ChangeUserEmailValidator()
        {
            RuleFor(x => x.UserId).ApplyValidGuidRule();
            RuleFor(x => x.NewEmail).ApplyUserEmailRules();
        }
    }
}
