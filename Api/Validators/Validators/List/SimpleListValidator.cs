using Api.Request.List;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.List
{
    public class SimpleListValidator : AbstractValidator<SimpleListRequest>
    {
        public SimpleListValidator() 
        { 
            RuleFor(x => x.PageNumber).ApplyPageNumberRules();
            RuleFor(x => x.PageSize).ApplyPageSizeRules();
        }
    }
}
