using Api.Request.Offer;
using Api.Validators.Rule;
using Domain.Constants;
using FluentValidation;

namespace Api.Validators.Validators.Offer
{
    public class ResendOfferEmailValidator : AbstractValidator<ResendOfferEmailRequest>
    {
        private static readonly string[] _allowedLanguages = { "pl", "en" };

        public ResendOfferEmailValidator()
        {
            RuleFor(x => x.OfferId)
                .ApplyValidGuidRule();

            RuleFor(x => x.Language)
                .Must(lang => string.IsNullOrWhiteSpace(lang) || _allowedLanguages.Contains(lang.ToLowerInvariant()))
                .WithMessage($"Language must be one of the following: {string.Join(", ", _allowedLanguages)}.")
                .WithErrorCode(ErrorCodes.InvalidOperation);
        }
    }
}
