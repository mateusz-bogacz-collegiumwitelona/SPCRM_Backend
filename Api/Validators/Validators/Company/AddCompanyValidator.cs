using Api.Request.Company;
using Api.Validators.Rule;
using Domain.Constants;
using Domain.Enum;
using FluentValidation;

namespace Api.Validators.Validators.Company
{
    public class AddCompanyValidator : AbstractValidator<AddCompanyRequest>
    {
        public AddCompanyValidator()
        {
            RuleFor(x => x.Name)
                .ApplyCompanyNameRules();

            RuleFor(x => x.NIP)
                .ApplyCompanyNipRules();

            RuleFor(x => x.Addresses)
                .NotEmpty().WithErrorCode(ErrorCodes.AddressRequired)
                .Must(addresses => addresses != null && addresses.Count(a =>
                    string.Equals(a.Type, nameof(AddressTypeEnum.Headquarters), StringComparison.OrdinalIgnoreCase)) == 1)
                .WithErrorCode(ErrorCodes.HeadquartersAddressRequired);

            RuleForEach(x => x.Addresses)
                .SetValidator(new AddCompanyAdressValidator());
        }
    }
}
