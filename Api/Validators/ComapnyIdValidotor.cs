using Domain.Constants;
using Api.Request;
using FluentValidation;

namespace Api.Validators
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
