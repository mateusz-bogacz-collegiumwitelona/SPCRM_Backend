using Api.Request.Company;
using Api.Validators.Rule;
using Domain.Constants;
using FluentValidation;

namespace Api.Validators.Validators.Company
{
    public class AddCompanyAdressRequestValidator : AbstractValidator<AddCompanyAdressRequest>
    {
        public AddCompanyAdressRequestValidator() 
        {
            RuleFor(x => x.Street).ApplyStreetRules();
            RuleFor(x => x.City).ApplyCityRules();
            RuleFor(x => x.ZipCode).ApplyZipCodeRules();
            RuleFor(x => x.Latitude).ApplyLatitudeRules();
            RuleFor(x => x.Longitude).ApplyLongitudeRules();
            RuleFor(x => x.Type).ApplyAddressTypeRules();
        }
    }
}
