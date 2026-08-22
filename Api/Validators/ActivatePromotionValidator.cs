using Api.Request;
using Api.Validators.Rule;
using Domain.Constants;
using FluentValidation;

namespace Api.Validators
{
    public class ActivatePromotionValidator : AbstractValidator<ActivatePromotionRequest>
    {
        public ActivatePromotionValidator()
        {
            RuleFor(x => x.Id).ApplyValidGuidRule();

            RuleFor(x => x.EndDate)
            .NotEmpty()
            .GreaterThan(DateTime.UtcNow)
            .WithErrorCode(ErrorCodes.InvalidDate);
        }
    }
}
