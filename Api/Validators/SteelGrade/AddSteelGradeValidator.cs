using Api.Request.SteelGrade;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.SteelGrade
{
    public class AddSteelGradeValidator : AbstractValidator<AddSteelGradeRequest>
    {
        public AddSteelGradeValidator()
        {
            RuleFor(x => x.Name)
                .ApplySteelGradeNameRules();

            RuleFor(x => x.Standard)
                .ApplySteelGradeStandardRules();

            RuleFor(x => x.Density)
                .ApplySteelGradeDensityRules();
        }
    }
}
