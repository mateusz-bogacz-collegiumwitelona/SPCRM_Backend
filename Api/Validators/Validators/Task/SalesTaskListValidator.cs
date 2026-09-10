using Api.Request.Task;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Task
{
    public class SalesTaskListValidator : AbstractValidator<SalesTaskListRequest>
    {
        public SalesTaskListValidator()
        {

            RuleFor(x => x.PageNumber).ApplyPageNumberRules();

            RuleFor(x => x.PageSize).ApplyPageSizeRules();
        }
    }
}
