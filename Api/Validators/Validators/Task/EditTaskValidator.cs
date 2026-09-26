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
                .When(x => x.Title != null);

            RuleFor(x => x.Description)
                .ApplyTaskDescriptionRules()
                .When(x => x.Description != null);

            RuleFor(x => x.Priority)
                .ApplyTaskPriorityRules()
                .When(x => x.Priority != null);
        }
    }
}
