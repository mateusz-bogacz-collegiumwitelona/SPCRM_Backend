using Api.Request;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators
{
    public class PaggedRequestValidator : AbstractValidator<PaggedRequest>
    {
        public PaggedRequestValidator()
        {
            RuleFor(x => x.PageSize).ApplyPageSizeRules();
            RuleFor(x => x.PageNumber).ApplyPageNumberRules();
        }
    }
}
