using Api.Request.Task;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Task
{
    public class AddTaskValidator : AbstractValidator<AddTaskRequest>
    {
        public AddTaskValidator()
        {
            RuleFor(x => x.Title).ApplyTaskTitleRules();
            RuleFor(x => x.Description).ApplyTaskDescriptionRules();
            RuleFor(x => x.DueAt).ApplyTaskDueAtRules();
            RuleFor(x => x.Priority).ApplyTaskPriorityRules();
            RuleFor(x => x.AssignedToId).ApplyValidGuidRule();
        }
    }
}
