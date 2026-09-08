using Domain.Constants;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using System.Linq.Expressions;

namespace Api.Validators.Rule
{
    public static class UserValiadtionRules
    {
        public static IRuleBuilderOptions<T, string> ApplyFirstNameRules<T>(this IRuleBuilder<T, string> ruleBuilder)
            => ruleBuilder
                .NotEmpty()
                .MaximumLength(50)
                .WithErrorCode(ErrorCodes.InvalidFirstName);

        public static IRuleBuilderOptions<T, string> ApplyLastNameRules<T>(this IRuleBuilder<T, string> ruleBuilder)
            => ruleBuilder
                .NotEmpty()
                .MaximumLength(50)
                .WithErrorCode(ErrorCodes.InvalidLastName);

        public static IRuleBuilderOptions<T, string> ApplyUserEmailRules<T>(this IRuleBuilder<T, string> ruleBuilder)
            => ruleBuilder
                .NotEmpty()
                .EmailAddress()
                .WithErrorCode(ErrorCodes.InvalidEmail);

        public static IRuleBuilderOptions<T, string> ApplyPasswordRules<T>(this IRuleBuilder<T, string> ruleBuilder)
            => ruleBuilder
            .NotEmpty()
            .MinimumLength(8)
            .Matches(@"[^a-zA-Z0-9]")
            .WithErrorCode(ErrorCodes.InvalidPassword);

        public static IRuleBuilderOptions<T, string> ApplyConfirmPasswordRules<T>(
            this IRuleBuilder<T, string> ruleBuilder,
            Expression<Func<T, string>> passwordExpression)
            => ruleBuilder
                .NotEmpty()
                .Equal(passwordExpression)
                .WithErrorCode(ErrorCodes.PassowrdMismatch);

        public static IRuleBuilderOptions<T, string> ApplyRoleRules<T, TRole>(
            this IRuleBuilder<T, string> ruleBuilder,
            RoleManager<TRole> roleManager) where TRole : class
            => ruleBuilder
                .NotEmpty()
                .WithErrorCode(ErrorCodes.InvalidRole)
                .MustAsync(async (role, _) =>
                {
                    if (string.IsNullOrWhiteSpace(role))
                    {
                        return false;
                    }

                    return await roleManager.RoleExistsAsync(role.Trim());
                })
                .WithErrorCode(ErrorCodes.InvalidRole);

    }
}
