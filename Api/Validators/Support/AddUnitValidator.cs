using Api.Request.Unit;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Support
{
    public class AddUnitValidator : AbstractValidator<AddUnitRequest>
    {
        public AddUnitValidator()
        {
            RuleFor(x => x.Name)
                .ApplyUnitNameRules();

            RuleFor(x => x.Symbol)
                .ApplyUnitSymbolRules();

            RuleFor(x => x.BaseMultiplier)
                .ApplyUnitBaseMultiplierRules();
        }
    }
}
