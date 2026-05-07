using Domain.Constants;
using DTO.Request;
using FluentValidation;

namespace Services.Validators
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
