using Api.Request.Auth;
using Domain.Constants;
using FluentValidation;

namespace Api.Validators.Validators.Login
{
    public class LoginValidator : AbstractValidator<LoginRequest>
    {
        public LoginValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty()
                .WithErrorCode(ErrorCodes.EmailRequired);

            RuleFor(x => x.Password)
                .NotEmpty()
                .WithErrorCode(ErrorCodes.PasswordRequired);
        }
    }
}
