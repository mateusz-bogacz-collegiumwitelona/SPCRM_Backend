using Api.Request;
using Domain.Constants;
using FluentValidation;

namespace Api.Validators
{
    public class CompanyIdValidator : AbstractValidator<GetCompanyIdRequest>
    {
        public CompanyIdValidator()
        {
            RuleFor(x => x.CompanyId)
                .NotEmpty()
                .NotEqual(Guid.Empty)
                .WithErrorCode(ErrorCodes.BadRequest);
        }
    }
}
