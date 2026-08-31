using Api.Request.Promotion;
using Api.Validators.Rule;
using Domain.Constants;
using FluentValidation;

namespace Api.Validators.Validators.Promotion
{
    public class PromotionListValidator : AbstractValidator<PromotionListRequest>
    {
        public PromotionListValidator()
        {
            RuleFor(x => x.PageNumber).ApplyPageNumberRules();
            RuleFor(x => x.PageSize).ApplyPageSizeRules();

            RuleFor(x => x.DiscountPrecentageFrom)
                .GreaterThanOrEqualTo(0)
                .When(x => x.DiscountPrecentageFrom.HasValue)
                .WithErrorCode(ErrorCodes.InvalidPromotionDiscount);

            RuleFor(x => x.DiscountPrecentageTo)
                .LessThanOrEqualTo(100)
                .GreaterThanOrEqualTo(x => x.DiscountPrecentageFrom ?? 0)
                .When(x => x.DiscountPrecentageTo.HasValue && x.DiscountPrecentageFrom.HasValue)
                .WithErrorCode(ErrorCodes.InvalidPromotionDiscount);

            RuleFor(x => x.PromotionPriceFrom)
                .GreaterThanOrEqualTo(0)
                .When(x => x.PromotionPriceFrom.HasValue)
                .WithErrorCode(ErrorCodes.InvalidPromotionPrice);

            RuleFor(x => x.PromotionPriceTo)
                .GreaterThanOrEqualTo(x => x.PromotionPriceFrom ?? 0)
                .When(x => x.PromotionPriceTo.HasValue && x.PromotionPriceFrom.HasValue)
                .WithErrorCode(ErrorCodes.InvalidPromotionPrice);

            RuleFor(x => x.ToDate)
                .GreaterThanOrEqualTo(x => x.FromDate)
                .When(x => x.ToDate.HasValue && x.FromDate.HasValue)
                .WithErrorCode(ErrorCodes.InvalidDate);

            var allowedSortColumns = new[] { "name", "startdate", "enddate", "discountpercentage", "promotionalprice" };
            RuleFor(x => x.SortBy)
                .Must(x => string.IsNullOrWhiteSpace(x) || allowedSortColumns.Contains(x.ToLower()))
                .WithErrorCode(ErrorCodes.InvalidSortColumn);
        }
    }
}
