using Domain.Constants;
using FluentValidation;

namespace Api.Validators.Rule
{
    public static class OfferValidationRules
    {
        public static IRuleBuilderOptions<T, Guid> ApplyOfferIdRules<T>(this IRuleBuilder<T, Guid> ruleBuilder)
            => ruleBuilder.ApplyValidGuidRule();

        public static IRuleBuilderOptions<T, DateTime?> ApplyNewValidUntilRules<T>(this IRuleBuilder<T, DateTime?> ruleBuilder)
            => ruleBuilder
                .Must(date => !date.HasValue || date.Value > DateTime.UtcNow)
                .WithErrorCode(ErrorCodes.InvalidDate);
    }
}
