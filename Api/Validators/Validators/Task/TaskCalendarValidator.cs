using Api.Request.Task;
using Api.Validators.Rule;
using Domain.Constants;
using FluentValidation;

namespace Api.Validators.Validators.Task
{
    public class TaskCalendarValidator : AbstractValidator<TaskCalendarRequest>
    {
        public TaskCalendarValidator()
        {
            RuleFor(x => x.DateTo)
                .GreaterThanOrEqualTo(x => x.DateFrom)
                .WithErrorCode(ErrorCodes.InvalidDate);

            RuleFor(x => x.TaskPriority)
                .ApplyTaskPriorityRules()
                .When(x => x.TaskPriority != null);

            RuleFor(x => x.TaskStatus)
                .ApplyTaskStatusRules()
                .When(x => x.TaskStatus != null);
        }
    }
}
