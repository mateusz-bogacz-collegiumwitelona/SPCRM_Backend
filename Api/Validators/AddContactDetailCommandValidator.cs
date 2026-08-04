using Api.Request;
using Domain.Constants;
using Domain.Enum;
using FluentValidation;

namespace Api.Validators
{
    public class AddContactDetailCommandValidator : AbstractValidator<AddContactDetailRequest>
    {
        public AddContactDetailCommandValidator()
        {
            RuleFor(x => x.Type)
                .IsEnumName(typeof(ContactDetailTypeEnum), caseSensitive: false)
                .WithErrorCode(ErrorCodes.TypeInvalid);

            RuleFor(x => x.Label)
                .NotEmpty()
                .WithErrorCode(ErrorCodes.LabelRequired)
                .Length(1, 50)
                .WithErrorCode(ErrorCodes.LabelLengthInvalid);

            When(x => x.Type.Equals("EMAIL", StringComparison.OrdinalIgnoreCase), () =>
            {
                RuleFor(x => x.Value)
                    .NotEmpty()
                    .WithErrorCode(ErrorCodes.EmailRequired)
                    .EmailAddress()
                    .WithErrorCode(ErrorCodes.EmailInvalid);
            });

            When(x => IsPhoneType(x.Type), () =>
            {
                RuleFor(x => x.Value)
                    .NotEmpty()
                    .WithErrorCode(ErrorCodes.NumberRequired)
                    .Must(BeAValidPhoneNumber)
                    .WithErrorCode(ErrorCodes.NumberInvalid);
            });

            When(x => x.Type.Equals("LINKEDIN", StringComparison.OrdinalIgnoreCase), () =>
            {
                RuleFor(x => x.Value)
                    .NotEmpty()
                    .WithErrorCode(ErrorCodes.LinkedInUrlRequired)
                    .Must(BeAValidLinkedInUrl)
                    .WithErrorCode(ErrorCodes.LinkedInUrlInvalid);
            });
        }

        private bool IsPhoneType(string type) =>
            type.Equals("PHONE", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("PHONE_MOBILE", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("FAX", StringComparison.OrdinalIgnoreCase);

        private bool BeAValidPhoneNumber(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            var digitsCount = value.Count(char.IsDigit);
            if (digitsCount < 7 || digitsCount > 15) return false;
            return value.All(c => char.IsDigit(c) || c == '+' || c == '-' || c == ' ' || c == '(' || c == ')');
        }

        private bool BeAValidLinkedInUrl(string value)
            => Uri.TryCreate(value, UriKind.Absolute, out var uriResult)
                && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps)
                && uriResult.Host.Contains("linkedin.com");
    }
}
