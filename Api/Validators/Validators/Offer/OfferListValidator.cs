using Api.Request.Offer;
using Api.Validators.Rule;
using Domain.Constants;
using Domain.Enum;
using FluentValidation;

namespace Api.Validators.Validators.Offer
{
    public class OfferListValidator : AbstractValidator<OfferListRequest>
    {
        public OfferListValidator()
        {
            RuleFor(x => x.PageNumber).ApplyPageNumberRules();
            RuleFor(x => x.PageSize).ApplyPageSizeRules();

            RuleFor(x => x.Status)
                .IsEnumName(typeof(OfferStatusEnum), caseSensitive: false)
                .WithErrorCode(ErrorCodes.InvalidOperation)
                .When(x => !string.IsNullOrWhiteSpace(x.Status));
        }
    }
}
