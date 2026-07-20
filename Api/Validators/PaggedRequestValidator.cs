using Domain.Constants;
using Api.Request;
using FluentValidation;

namespace Api.Validators
{
    public class PaggedRequestValidator : AbstractValidator<PaggedRequest>
    {
        public PaggedRequestValidator()
        {
            RuleFor(x => x.PageSize)
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.ValidationError);

            RuleFor(x => x.PageNumber)
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.ValidationError);
        }
    }
}
