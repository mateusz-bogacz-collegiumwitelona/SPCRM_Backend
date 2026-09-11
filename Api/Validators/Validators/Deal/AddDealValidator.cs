using Api.Request.Deal;
using Api.Validators.Rule;
using Domain.Constants;
using FluentValidation;

namespace Api.Validators.Validators.Deal
{
    public class AddDealValidator : AbstractValidator<AddDealRequest>
    {
        public AddDealValidator()
        {

            RuleFor(x => x.CloseDate)
                .ApplyDealCloseDateRules();

            RuleFor(x => x.CurrencyId)
                .ApplyValidGuidRule();

            RuleFor(x => x.CompanyId)
                .ApplyValidGuidRule();

            RuleFor(x => x.Products)
                .NotEmpty().WithErrorCode(ErrorCodes.InvalidOperation);

            RuleForEach(x => x.Products).SetValidator(new AddDealProductValidator());
        }
    }
}
