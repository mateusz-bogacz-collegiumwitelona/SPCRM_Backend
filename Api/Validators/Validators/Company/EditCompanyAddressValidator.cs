using Api.Request.Company;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Company
{
    public class EditCompanyAddressValidator : AbstractValidator<EditCompanyAdressRequest>
    {
        public EditCompanyAddressValidator()
        {
            RuleFor(x => x.AddressId)
                .ApplyValidGuidRule();

            RuleFor(x => x.Street)
                .ApplyCompanyStreetRules()
                .When(x => !string.IsNullOrEmpty(x.Street));

            RuleFor(x => x.City)
                .ApplyCompanyCityRules()
                .When(x => !string.IsNullOrEmpty(x.City));

            RuleFor(x => x.ZipCode)
                .ApplyCompanyZipCodeRules()
                .When(x => !string.IsNullOrEmpty(x.ZipCode));

            RuleFor(x => x.Latitude)
                .ApplyCompanyLatitudeRules()
                .When(x => x.Latitude.HasValue);

            RuleFor(x => x.Longitude)
                .ApplyCompanyLongitudeRules()
                .When(x => x.Longitude.HasValue);

            RuleFor(x => x.Type)
                .ApplyCompanyAddressTypeRules()
                .When(x => !string.IsNullOrEmpty(x.Type));
        }
    }
}
