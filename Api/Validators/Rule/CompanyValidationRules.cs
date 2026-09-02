using Domain.Constants;
using Domain.Enum;
using FluentValidation;

namespace Api.Validators.Rule
{
    public static class CompanyValidationRules
    {
        public static IRuleBuilderOptions<T, string> ApplyCompanyNameRules<T>(this IRuleBuilder<T, string> ruleBuilder)
            => ruleBuilder
                .NotEmpty().WithErrorCode(ErrorCodes.NameRequired)
                .MaximumLength(100).WithErrorCode(ErrorCodes.NameLengthInvalid);

        public static IRuleBuilderOptions<T, string> ApplyStreetRules<T>(this IRuleBuilder<T, string> ruleBuilder)
            => ruleBuilder
                .NotEmpty().WithErrorCode(ErrorCodes.StreetRequired)
                .MaximumLength(200).WithErrorCode(ErrorCodes.StreetLengthInvalid);

        public static IRuleBuilderOptions<T, string> ApplyCityRules<T>(this IRuleBuilder<T, string> ruleBuilder)
            => ruleBuilder
                .NotEmpty().WithErrorCode(ErrorCodes.CityRequired)
                .MaximumLength(100).WithErrorCode(ErrorCodes.CityLengthInvalid);

        public static IRuleBuilderOptions<T, string> ApplyNipRules<T>(this IRuleBuilder<T, string> ruleBuilder)
            => ruleBuilder
                .NotEmpty().WithErrorCode(ErrorCodes.NipRequired)
                .Must(BeValidNipChecksum).WithErrorCode(ErrorCodes.NipNotValid);

        public static IRuleBuilderOptions<T, string> ApplyZipCodeRules<T>(this IRuleBuilder<T, string> ruleBuilder)
            => ruleBuilder
                .NotEmpty().WithErrorCode(ErrorCodes.ZipCodeRequired)
                .Matches(@"^\d{2}-\d{3}$").WithErrorCode(ErrorCodes.ZipCodeNotValid);

        public static IRuleBuilderOptions<T, float> ApplyLatitudeRules<T>(this IRuleBuilder<T, float> ruleBuilder)
            => ruleBuilder
                .InclusiveBetween(-90f, 90f)
                .WithErrorCode(ErrorCodes.LatitudeOutOfRange);

        public static IRuleBuilderOptions<T, float> ApplyLongitudeRules<T>(this IRuleBuilder<T, float> ruleBuilder)
            => ruleBuilder
                .InclusiveBetween(-180f, 180f)
                .WithErrorCode(ErrorCodes.LongitudeOutOfRange);

        public static IRuleBuilderOptions<T, string> ApplyAddressTypeRules<T>(this IRuleBuilder<T, string> ruleBuilder)
            => ruleBuilder
                .NotEmpty().WithErrorCode(ErrorCodes.AddressTypeRequired)
                .Must(type => Enum.TryParse<AddressTypeEnum>(type, true, out _))
                .WithErrorCode(ErrorCodes.AddressTypeNotInvalid);

        private static bool BeValidNipChecksum(string nip)
        {
            if (string.IsNullOrWhiteSpace(nip)) return false;

            var clean = nip.Replace("-", "").Replace(" ", "").Trim().ToUpperInvariant();
            if (clean.StartsWith("PL")) clean = clean[2..];

            if (clean.Length != 10 || !clean.All(char.IsDigit)) return false;
            

            int[] weights = { 6, 5, 7, 2, 3, 4, 5, 6, 7 };
            int sum = 0;

            for (int i = 0; i < 9; i++) sum += (clean[i] - '0') * weights[i];

            int controlDigit = sum % 11;
            return controlDigit != 10 && controlDigit == (clean[9] - '0');
        }
    }
}
