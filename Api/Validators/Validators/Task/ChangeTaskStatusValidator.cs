using Api.Request.Task;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Task
{
    public class ChangeTaskStatusValidator : AbstractValidator<ChangeTaskStatusRequest>
    {
        public ChangeTaskStatusValidator()
        {
            RuleFor(x => x.TaskId).ApplyValidGuidRule();

            RuleFor(x => x.Status).ApplyTaskStatusRules();
        }

    }

}
