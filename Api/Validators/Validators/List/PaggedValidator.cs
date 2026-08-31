using Api.Request.List;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.List
{
    public class PaggedValidator : AbstractValidator<PaggedRequest>
    {
        public PaggedValidator()
        {
            RuleFor(x => x.PageSize).ApplyPageSizeRules();
            RuleFor(x => x.PageNumber).ApplyPageNumberRules();
        }
    }
}
