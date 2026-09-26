using Domain.Constants;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using System.Linq.Expressions;

namespace Api.Validators.Rule
{
    public static class UserValidationRules
    {
        public static IRuleBuilderOptions<T, string?> ApplyFirstNameRules<T>(this IRuleBuilder<T, string?> ruleBuilder)
          => ruleBuilder
              .NotEmpty().WithErrorCode(ErrorCodes.InvalidFirstName)
              .MaximumLength(50).WithErrorCode(ErrorCodes.InvalidFirstName);

        public static IRuleBuilderOptions<T, string?> ApplyLastNameRules<T>(this IRuleBuilder<T, string?> ruleBuilder)
            => ruleBuilder
                .NotEmpty().WithErrorCode(ErrorCodes.InvalidLastName)
                .MaximumLength(50).WithErrorCode(ErrorCodes.InvalidLastName);

        public static IRuleBuilderOptions<T, string?> ApplyRequiredEmailRules<T>(this IRuleBuilder<T, string?> ruleBuilder)
          => ruleBuilder
              .NotEmpty()
              .WithErrorCode(ErrorCodes.EmailRequired)
              .EmailAddress()
              .WithErrorCode(ErrorCodes.InvalidEmail);

        public static IRuleBuilderOptions<T, string?> ApplyOptionalEmailRules<T>(this IRuleBuilder<T, string?> ruleBuilder)
            => ruleBuilder
                .Must(email => string.IsNullOrWhiteSpace(email) || BeValidEmail(email))
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

        public static IRuleBuilderOptions<T, string> ApplyValidTokenRule<T>(this IRuleBuilder<T, string> ruleBuilder)
            => ruleBuilder
                .NotEmpty()
                .WithErrorCode(ErrorCodes.TokenInvalid);

        private static bool BeValidEmail(string email)
           => new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email);
    }
}
