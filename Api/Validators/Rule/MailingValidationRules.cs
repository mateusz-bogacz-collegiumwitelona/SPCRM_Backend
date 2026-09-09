using Domain.Constants;
using FluentValidation;

namespace Api.Validators.Rule
{
    public static class MailingValidationRules
    {
        private static readonly string[] _allowedLanguages = { "pl", "en" };

        public static IRuleBuilderOptions<T, string?> ApplyLanguageRules<T>(this IRuleBuilder<T, string?> ruleBuilder)
            => ruleBuilder
                .Must(lang => string.IsNullOrWhiteSpace(lang) || _allowedLanguages.Contains(lang.ToLowerInvariant()))
                .WithErrorCode(ErrorCodes.InvalidOperation);
    }
}
