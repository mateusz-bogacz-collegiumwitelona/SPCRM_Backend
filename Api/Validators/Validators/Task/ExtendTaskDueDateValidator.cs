using Api.Request.Task;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Task
{
    public class ExtendTaskDueDateValidator : AbstractValidator<ExtendTaskDueDateRequest>
    {
        public ExtendTaskDueDateValidator() 
        {
            RuleFor(x => x.NewDueDate).ApplyTaskDueAtRules();
        }
    }
}
