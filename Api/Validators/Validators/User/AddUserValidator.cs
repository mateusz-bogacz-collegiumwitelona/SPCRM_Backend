using Api.Request.User;
using Api.Validators.Rule;
using FluentValidation;
using Microsoft.AspNetCore.Identity;

namespace Api.Validators.Validators.User
{
    public class AddUserValidator : AbstractValidator<AddUserRequest>
    {
        public AddUserValidator(RoleManager<IdentityRole<Guid>> roleManager)
        {
            RuleFor(x => x.FirstName).ApplyFirstNameRules();
            RuleFor(x => x.LastName).ApplyLastNameRules();
            RuleFor(x => x.Email).ApplyUserEmailRules();
            RuleFor(x => x.Password).ApplyPasswordRules();
            RuleFor(x => x.ConfirmPassword).ApplyConfirmPasswordRules(x => x.Password);
            RuleFor(x => x.Role).ApplyRoleRules(roleManager);
        }
    }
}
