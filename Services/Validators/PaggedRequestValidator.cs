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
                .LessThanOrEqualTo(0)
                .WithErrorCode(ErrorCodes.ValidationError);

            RuleFor(x => x.PageNumber)
                .LessThanOrEqualTo(0)
                .WithErrorCode(ErrorCodes.ValidationError);
        }
    }
}
