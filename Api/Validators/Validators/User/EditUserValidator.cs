using Api.Request.User;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.User
{
    public class EditUserValidator : AbstractValidator<EditUserRequest>
    {
        public EditUserValidator()
        {
            RuleFor(x => x.UserId).ApplyValidGuidRule();
            RuleFor(x => x.FirstName).ApplyFirstNameRules().When(x => x.FirstName != null); ;
            RuleFor(x => x.LastName).ApplyLastNameRules().When(x => x.LastName != null); ;
        }
    }
}
