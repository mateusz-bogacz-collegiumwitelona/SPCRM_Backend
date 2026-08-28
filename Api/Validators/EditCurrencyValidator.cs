using Api.Request;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators
{
    public class EditCurrencyValidator : AbstractValidator<EditCurrencyRequest>
    {
        public EditCurrencyValidator()
        {
            RuleFor(x => x.CurrencyId)
                .ApplyValidGuidRule();

            RuleFor(x => x.Name)
                .ApplyCurrencyNameRules()
                .When(x => x.Name != null);

            RuleFor(x => x.Code)
                .ApplyCurrencyCodeRules()
                .When(x => x.Code != null);

            RuleFor(x => x.DecimalPlaces)
                .ApplyCurrencyDecimalPlacesRules()
                .When(x => x.DecimalPlaces.HasValue);
        }
    }
}
