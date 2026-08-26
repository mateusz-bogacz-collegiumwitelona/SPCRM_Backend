using Api.Request;
using Domain.Constants;
using FluentValidation;

namespace Api.Validators
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
