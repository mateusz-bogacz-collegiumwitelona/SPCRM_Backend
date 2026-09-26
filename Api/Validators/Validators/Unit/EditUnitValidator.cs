using Api.Request.Unit;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Unit
{
    public class EditUnitValidator : AbstractValidator<EditUnitRequest>
    {
        public EditUnitValidator()
        {
            RuleFor(x => x.UnitId)
                .ApplyValidGuidRule();

            RuleFor(x => x.Name)
                .ApplyUnitNameRules()
                .When(x => x.Name != null);

            RuleFor(x => x.Symbol)
                .ApplyUnitSymbolRules()
                .When(x => x.Symbol != null);

            RuleFor(x => x.BaseMultiplier)
                .ApplyUnitBaseMultiplierRules()
                .When(x => x.BaseMultiplier.HasValue);
        }
    }
}
