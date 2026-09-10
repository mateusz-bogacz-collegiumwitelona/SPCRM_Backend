using Api.Request.Task;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Task
{
    public class UserTaskListValidator : AbstractValidator<UserTaskListRequest>
    {
        public UserTaskListValidator()
        {
            RuleFor(x => x.PageNumber).ApplyPageNumberRules();

            RuleFor(x => x.PageSize).ApplyPageSizeRules();
        }
    }
}
