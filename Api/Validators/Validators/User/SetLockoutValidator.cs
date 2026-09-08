using Api.Request.User;
using Api.Validators.Rule;
using Domain.Constants;
using FluentValidation;

namespace Api.Validators.Validators.User
{
    public class SetLockoutValidator : AbstractValidator<SetLockoutRequest>
    {
        public SetLockoutValidator()
        {
            RuleFor(x => x.UserId).ApplyValidGuidRule();

            RuleFor(x => x.LockoutEnd)
                .Must(date => !date.HasValue || date.Value > DateTime.UtcNow)
                .WithErrorCode(ErrorCodes.InvalidDate);
        }
    }
}
