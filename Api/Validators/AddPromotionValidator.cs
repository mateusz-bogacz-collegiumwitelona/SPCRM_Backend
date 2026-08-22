using Api.Request;
using Api.Validators.Rule;
using Domain.Constants;
using FluentValidation;

namespace Api.Validators
{
    public class AddPromotionValidator : AbstractValidator<AddPromotionRequest>
    {
        public AddPromotionValidator()
        {
            RuleFor(x => x.ProductId).ApplyValidGuidRule();

            RuleFor(x => x.Name).ApplyPromotionNameRules();

            RuleFor(x => x)
            .Must(x => (x.DiscountPercentage.HasValue && !x.PromotionalPrice.HasValue)
                    || (!x.DiscountPercentage.HasValue && x.PromotionalPrice.HasValue))
            .WithErrorCode(ErrorCodes.DiscountPercentageAndPriceCannotBothChoice);

            RuleFor(x => x.DiscountPercentage).ApplyDiscountPercentageRule();

            RuleFor(x => x.PromotionalPrice).ApplyPromotionalPriceRule();

            RuleFor(x => x.CurrencyId)
                .NotNull()
                .WithErrorCode(ErrorCodes.GuidRequired)
                .ApplyOptionalGuidRule()
                .When(x => x.PromotionalPrice.HasValue);

            RuleFor(x => x.ContactId)
                .ApplyOptionalGuidRule()
                .When(x => x.ContactId.HasValue);

            RuleFor(x => x)
                .Must(x => !x.StartDate.HasValue || !x.EndDate.HasValue || x.EndDate >= x.StartDate)
                .WithErrorCode(ErrorCodes.InvalidDate);

            RuleFor(x => x.MinQuantity)
                .ApplyMinQuantityRules()
                .When(x => x.MinQuantity.HasValue);

            RuleFor(x => x.MinWeight)
                .ApplyMinWeightRules()
                .When(x => x.MinWeight.HasValue);
        }
    }
}
