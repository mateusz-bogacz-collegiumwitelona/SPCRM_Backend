using Domain.Constants;
using Api.Request;
using FluentValidation;

namespace Api.Validators
{
    public class LoginRequestValidator : AbstractValidator<LoginRequest>
    {
        public LoginRequestValidator()
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
