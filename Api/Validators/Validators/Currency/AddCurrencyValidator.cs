using Api.Request.Currency;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Currency
{
    public class AddCurrencyValidator : AbstractValidator<AddCurrencyRequest>
    {
        public AddCurrencyValidator()
        {
            RuleFor(x => x.Name)
                .ApplyCurrencyNameRules();

            RuleFor(x => x.Code)
                .ApplyCurrencyCodeRules();

            RuleFor(x => x.DecimalPlaces);
        }
    }
}
