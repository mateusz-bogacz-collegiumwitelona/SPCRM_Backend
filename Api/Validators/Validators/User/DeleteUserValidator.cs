using Api.Request.User;
using Api.Validators.Rule;
using Domain.Constants;
using FluentValidation;

namespace Api.Validators.Validators.User
{
    public class DeleteUserValidator : AbstractValidator<DeleteUserRequest>
    {
        public DeleteUserValidator()
        {
            RuleFor(x => x.UserId).ApplyValidGuidRule();

            RuleFor(x => x.ReassignToUserId)
                .ApplyValidGuidRule()
                .NotEqual(x => x.UserId)
                .WithErrorCode(ErrorCodes.InvalidOperation);
        }
    }
}
