using Api.Request.Mailing;
using Api.Validators.Rule;
using Domain.Constants;
using FluentValidation;

namespace Api.Validators.Validators.Mailing
{
    public class MailingProductValidator : AbstractValidator<MailingProductRequest>
    {
        public MailingProductValidator()
        {
            RuleFor(x => x.ProductId).ApplyValidGuidRule();
            RuleFor(x => x.CurrencyCode)
                .ApplyCurrencyCodeRules()
                .When(x => x.CurrencyCode != null);

            RuleFor(x => x.Price)
                .GreaterThan(0)
                .When(x => x.Price.HasValue)
                .WithErrorCode(ErrorCodes.InvalidMalingPrice);

            RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithErrorCode(ErrorCodes.InvalidMalingQuantity);
        }
    }
}
