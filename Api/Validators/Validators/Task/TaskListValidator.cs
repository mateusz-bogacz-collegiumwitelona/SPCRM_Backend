using Api.Request.Task;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Task
{
    public class TaskListValidator : AbstractValidator<TaskListRequest>
    {
        public TaskListValidator()
        {
            RuleFor(x => x.PageNumber).ApplyPageNumberRules();

            RuleFor(x => x.PageSize).ApplyPageSizeRules();
        }
    }
}
