using Api.Request.Task;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Task
{
    public class EditTaskValidator : AbstractValidator<EditTaskRequest>
    {
        public EditTaskValidator()
        {

            RuleFor(x => x.Title)
                .ApplyTaskTitleRules()
                .When(x => !string.IsNullOrEmpty(x.Title), ApplyConditionTo.CurrentValidator);

            RuleFor(x => x.Description)
                .ApplyTaskDescriptionRules()
                .When(x => !string.IsNullOrEmpty(x.Description), ApplyConditionTo.CurrentValidator);

            RuleFor(x => x.Priority)
                .ApplyTaskPriorityRules()
                .When(x => x.Priority != null, ApplyConditionTo.CurrentValidator);
        }
    }
}
