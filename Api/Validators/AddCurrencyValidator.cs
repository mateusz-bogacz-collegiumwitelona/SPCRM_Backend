using Api.Request;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators
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
