using Api.Request;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators
{
    public class EditSteelGradeValidator : AbstractValidator<EditSteelGradeRequest>
    {
        public EditSteelGradeValidator()
        {
            RuleFor(x => x.Id)
                .ApplyValidGuidRule();

            RuleFor(x => x.Name)
                .ApplySteelGradeNameRules()
                .When(x => x.Name != null);

            RuleFor(x => x.Standard)
                .ApplySteelGradeStandardRules()
                .When(x => x.Standard != null);

            RuleFor(x => x.Density)
                .ApplySteelGradeDensityRules()
                .When(x => x.Density.HasValue);
        }
    }
}
