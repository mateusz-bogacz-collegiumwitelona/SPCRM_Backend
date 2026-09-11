using Api.Request.Deal;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Task
{
    public class AddDealTaskValidator : AbstractValidator<AddDealTaskRequest>
    {
        public AddDealTaskValidator()
        {
            RuleFor(x => x.Title).ApplyTaskTitleRules();
            RuleFor(x => x.Description).ApplyTaskDescriptionRules();
            RuleFor(x => x.DueAt).ApplyTaskDueAtRules();
            RuleFor(x => x.Priority).ApplyTaskPriorityRules();
            RuleFor(x => x.AssignedToId).ApplyValidGuidRule();
        }
    }
}
