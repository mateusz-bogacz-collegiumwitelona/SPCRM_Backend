using Riok.Mapperly.Abstractions;
using System.Globalization;

namespace Api.Mappers.Helper
{
    public abstract class BaseMapper
    {
        private static readonly TextInfo _textInfo = CultureInfo.InvariantCulture.TextInfo;

        [UserMapping(Default = false)]
        protected static string? NormalizeNullableName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            var trimmed = value.Trim();
            return _textInfo.ToTitleCase(trimmed.ToLowerInvariant());
        }

        [UserMapping(Default = false)]
        protected static string NormalizeName(string? value)
            => string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : _textInfo.ToTitleCase(value.Trim().ToLowerInvariant());

        [UserMapping(Default = false)]
        protected static string Trim(string? value)
            => string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();

        [UserMapping(Default = false)]
        protected static string? TrimAndUpper(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

        [UserMapping(Default = false)]
        protected static string? TrimAndLower(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

        [UserMapping(Default = false)]
        protected static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
    }
}
