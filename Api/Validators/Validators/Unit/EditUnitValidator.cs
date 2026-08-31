using Api.Request.Unit;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Unit
{
    public class EditUnitValidator : AbstractValidator<EditUnitReqeust>
    {
        public EditUnitValidator()
        {
            RuleFor(x => x.UnitId)
                .ApplyValidGuidRule();

            RuleFor(x => x.Name)
                .ApplyUnitNameRules();

            RuleFor(x => x.Symbol)
                .ApplyUnitSymbolRules();

            RuleFor(x => x.BaseMultiplier)
                .ApplyUnitBaseMultiplierRules();
        }
    }
}
