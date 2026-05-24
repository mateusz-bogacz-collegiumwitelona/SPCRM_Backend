using Domain.Constants;
using DTO.Request;
using FluentValidation;

namespace Services.Validators
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
