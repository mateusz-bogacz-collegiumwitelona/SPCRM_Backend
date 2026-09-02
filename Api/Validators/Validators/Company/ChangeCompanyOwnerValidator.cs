using Api.Request.Company;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Company
{
    public class ChangeCompanyOwnerValidator : AbstractValidator<ChangeCompanyOwnerRequest>
    {
        public ChangeCompanyOwnerValidator()
        {
            RuleFor(x => x.CompanyId)
                .ApplyValidGuidRule();

            RuleFor(x => x.UserId)
                .ApplyValidGuidRule();
        }
    }
}
