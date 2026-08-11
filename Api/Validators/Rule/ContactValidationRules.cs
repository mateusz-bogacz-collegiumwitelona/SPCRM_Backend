using Domain.Constants;
using Domain.Enum;
using FluentValidation;

namespace Api.Validators.Rule
{
    public static class ContactValidationRules
    {
        public static IRuleBuilderOptions<T, string?> ApplyNameRules<T>(this IRuleBuilder<T, string?> ruleBuilder)
              => ruleBuilder
                  .Length(5, 100)
                  .WithErrorCode(ErrorCodes.NameLengthInvalid);

        public static IRuleBuilderOptions<T, string?> ApplyTypeRules<T>(this IRuleBuilder<T, string?> ruleBuilder)
            => ruleBuilder
                .IsEnumName(typeof(ContactDetailTypeEnum), caseSensitive: false)
                .WithErrorCode(ErrorCodes.TypeInvalid);

        public static IRuleBuilderOptions<T, string?> ApplyLabelRules<T>(this IRuleBuilder<T, string?> ruleBuilder)
            => ruleBuilder
                .Length(1, 50)
                .WithErrorCode(ErrorCodes.LabelLengthInvalid);

        public static IRuleBuilderOptions<T, string?> ApplyEmailRules<T>(this IRuleBuilder<T, string?> ruleBuilder)
            => ruleBuilder
                .EmailAddress()
                .WithErrorCode(ErrorCodes.EmailInvalid);

        public static IRuleBuilderOptions<T, string?> ApplyPhoneRules<T>(this IRuleBuilder<T, string?> ruleBuilder)
            => ruleBuilder
                .Must(BeAValidPhoneNumber)
                .WithErrorCode(ErrorCodes.NumberInvalid);

        public static IRuleBuilderOptions<T, string?> ApplyLinkedInRules<T>(this IRuleBuilder<T, string?> ruleBuilder)
            => ruleBuilder
                .Must(BeAValidLinkedInUrl)
                .WithErrorCode(ErrorCodes.LinkedInUrlInvalid);

        public static bool IsPhoneType(string? type) =>
            !string.IsNullOrEmpty(type) && (
            type.Equals("PHONE", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("PHONE_MOBILE", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("FAX", StringComparison.OrdinalIgnoreCase));

        private static bool BeAValidPhoneNumber(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            var digitsCount = value.Count(char.IsDigit);
            if (digitsCount < 7 || digitsCount > 15) return false;
            return value.All(c => char.IsDigit(c) || c == '+' || c == '-' || c == ' ' || c == '(' || c == ')');
        }

        private static bool BeAValidLinkedInUrl(string? value)
            => Uri.TryCreate(value, UriKind.Absolute, out var uriResult)
                && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps)
                && uriResult.Host.Contains("linkedin.com");
    }
}
