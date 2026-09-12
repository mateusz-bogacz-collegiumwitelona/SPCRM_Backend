using Domain.Constants;
using FluentValidation;

namespace Api.Validators.Rule
{
    public static class GlobalValidationRules
    {
        private static readonly string[] _allowedLanguages = { "pl", "en" };

        public static IRuleBuilderOptions<T, Guid> ApplyValidGuidRule<T>(
            this IRuleBuilder<T, Guid> ruleBuilder,
            string errorCode = ErrorCodes.ValidationError)
            => ruleBuilder
                .NotEmpty()
                .WithErrorCode(ErrorCodes.GuidRequired)
                .NotEqual(Guid.Empty)
                .WithErrorCode(ErrorCodes.GuidInvalid);

        public static IRuleBuilderOptions<T, Guid?> ApplyValidGuidRule<T>(
            this IRuleBuilder<T, Guid?> ruleBuilder,
            string errorCode = ErrorCodes.ValidationError)
            => ruleBuilder
                .Must(id => !id.HasValue || id.Value != Guid.Empty)
                .WithErrorCode(ErrorCodes.GuidInvalid);

        public static IRuleBuilderOptions<T, int?> ApplyPageNumberRules<T>(
            this IRuleBuilder<T, int?> ruleBuilder,
            string errorCode = ErrorCodes.ValidationError)
                => ruleBuilder
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.PageNumberInvalid);

        public static IRuleBuilderOptions<T, int?> ApplyPageSizeRules<T>(
            this IRuleBuilder<T, int?> ruleBuilder,
            string errorCode = ErrorCodes.ValidationError)
                => ruleBuilder
                    .GreaterThan(0)
                    .LessThanOrEqualTo(100)
                    .WithErrorCode(ErrorCodes.PageSizeInvalid);

        public static IRuleBuilderOptions<T, string?> ApplyLanguageRules<T>(this IRuleBuilder<T, string?> ruleBuilder)
            => ruleBuilder
                .Must(lang => string.IsNullOrWhiteSpace(lang) || _allowedLanguages.Contains(lang.ToLowerInvariant()))
                .WithErrorCode(ErrorCodes.InvalidOperation);
    }
}
