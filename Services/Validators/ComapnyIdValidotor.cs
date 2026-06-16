using Domain.Constants;
using DTO.Request;
using FluentValidation;

namespace Services.Validators
{
    public class CompanyIdValidator : AbstractValidator<GetCompanyIdRequest>
    {
        public CompanyIdValidator()
        {
                RuleFor(x => x.CompanyId).NotEmpty().WithErrorCode(ErrorCodes.BadRequest)
                .NotEqual(Guid.Empty).WithErrorCode(ErrorCodes.BadRequest);
        }
    }
}
