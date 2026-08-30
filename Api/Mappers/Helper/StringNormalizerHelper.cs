using System.Globalization;

namespace Api.Mappers.Helper
{
    public static class StringNormalizerHelper
    {
        private static readonly TextInfo _textInfo = CultureInfo.InvariantCulture.TextInfo;

        public static string? NormalizeName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            return _textInfo.ToTitleCase(trimmed.ToLowerInvariant());
        }

        public static string? Trim(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        public static string? TrimAndUpper(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

        public static string? TrimAndLower(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    }
}
