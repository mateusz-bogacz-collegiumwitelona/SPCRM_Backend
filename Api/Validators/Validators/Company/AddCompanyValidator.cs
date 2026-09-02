using Api.Request.Company;
using Api.Validators.Rule;
using Api.Validators.Validators.Company;
using Domain.Constants;
using Domain.Enum;
using FluentValidation;

public class AddCompanyValidator : AbstractValidator<AddCompanyRequest>
{
    public AddCompanyValidator()
    {
        RuleFor(x => x.Name)
            .ApplyCompanyNameRules();

        RuleFor(x => x.NIP)
            .ApplyCompanyNipRules();

        RuleFor(x => x.Address)
            .NotEmpty().WithErrorCode(ErrorCodes.AddressRequired)
            .Must(addresses => addresses != null && addresses.Count(a =>
                string.Equals(a.Type, nameof(AddressTypeEnum.Headquarters), StringComparison.OrdinalIgnoreCase)) == 1)
            .WithErrorCode(ErrorCodes.HeadquartersAddressRequired);

        RuleForEach(x => x.Address)
            .SetValidator(new AddCompanyAdressValidator());
    }
}
