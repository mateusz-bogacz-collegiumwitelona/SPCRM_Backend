using Api.Request.Company;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Company
{
    public class EditCompanyValidator : AbstractValidator<EditCompanyRequest>
    {
        public EditCompanyValidator()
        {
            RuleFor(x => x.Id)
                .ApplyValidGuidRule();

            RuleFor(x => x.Name)
                .ApplyCompanyNameRules()
                .When(x => x.Name != null);

            RuleFor(x => x.NIP)
                .ApplyCompanyNipRules()
                .When(x => x.NIP != null);
        }
    }
}
