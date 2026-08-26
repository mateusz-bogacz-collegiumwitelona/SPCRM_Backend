using Api.Request;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators
{
    public class BasicListValitator : AbstractValidator<BasicListRequest>
    {
        public BasicListValitator()
        {
            RuleFor(x => x.PageNumber)
                .ApplyPageNumberRules();

            RuleFor(x => x.PageSize)
                .ApplyPageSizeRules();
        }
    }
}
