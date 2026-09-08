using Api.Request.User;
using Api.Validators.Rule;
using FluentValidation;
using Microsoft.AspNetCore.Identity;

namespace Api.Validators.Validators.User
{
    public class ChangeRoleValidator : AbstractValidator<ChangeRoleRequest>
    {
        public ChangeRoleValidator(RoleManager<IdentityRole<Guid>> roleManager)
        {
            RuleFor(x => x.UserId).ApplyValidGuidRule();
            RuleFor(x => x.Role).ApplyRoleRules(roleManager);
        }
    }
}
