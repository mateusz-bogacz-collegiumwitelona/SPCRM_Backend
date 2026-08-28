using Api.Request.Currency;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Currency
{
    public class AddCurrencyValidator : AbstractValidator<AddCurrencyRequest>
    {
        public AddCurrencyValidator()
        {
            RuleFor(x => x.Name)
                .ApplyCurrencyNameRules()
                .When(x => !string.IsNullOrWhiteSpace(x.Name));

            RuleFor(x => x.Code)
                .ApplyCurrencyCodeRules()
                .When(x => !string.IsNullOrWhiteSpace(x.Code));

            RuleFor(x => x.DecimalPlaces)
                .ApplyCurrencyDecimalPlacesRules();
        }
    }
}
