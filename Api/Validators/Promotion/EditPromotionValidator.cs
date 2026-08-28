using Api.Request.Promotion;
using Api.Validators.Rule;
using Domain.Constants;
using FluentValidation;

namespace Api.Validators.Promotion
{
    public class EditPromotionValidator : AbstractValidator<EditPromotionRequest>
    {
        public EditPromotionValidator()
        {
            RuleFor(x => x.Id).ApplyPromotionIdRules();

            RuleFor(x => x.Name!)
                .ApplyPromotionNameRules()
                .When(x => x.Name != null);

            RuleFor(x => x)
                .Must(x => !(x.DiscountPercentage.HasValue && x.PromotionalPrice.HasValue))
                .WithErrorCode(ErrorCodes.DiscountPercentageAndPriceCannotBothChoice);

            RuleFor(x => x.DiscountPercentage)
                .ApplyDiscountPercentageRule()
                .When(x => x.DiscountPercentage.HasValue);

            RuleFor(x => x.PromotionalPrice)
                .ApplyPromotionalPriceRule()
                .When(x => x.PromotionalPrice.HasValue);

            RuleFor(x => x.CurrencyId)
                .NotNull()
                .WithErrorCode(ErrorCodes.GuidRequired)
                .ApplyOptionalGuidRule()
                .When(x => x.PromotionalPrice.HasValue);

            RuleFor(x => x.ContactId)
                .ApplyOptionalGuidRule()
                .When(x => x.ContactId.HasValue);

            RuleFor(x => x.MinQuantity)
                .ApplyMinQuantityRules()
                .When(x => x.MinQuantity.HasValue);

            RuleFor(x => x.MinWeight)
                .ApplyMinWeightRules()
                .When(x => x.MinWeight.HasValue);
        }
    }
}
