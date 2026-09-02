using Api.Request.Company;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Company
{
    public class AddCompanyAdressValidator : AbstractValidator<AddCompanyAdressRequest>
    {
        public AddCompanyAdressValidator()
        {
            RuleFor(x => x.Street).ApplyCompanyStreetRules();
            RuleFor(x => x.City).ApplyCompanyCityRules();
            RuleFor(x => x.ZipCode).ApplyCompanyZipCodeRules();
            RuleFor(x => x.Latitude).ApplyCompanyLatitudeRules();
            RuleFor(x => x.Longitude).ApplyCompanyLongitudeRules();
            RuleFor(x => x.Type).ApplyCompanyAddressTypeRules();
        }
    }
}
