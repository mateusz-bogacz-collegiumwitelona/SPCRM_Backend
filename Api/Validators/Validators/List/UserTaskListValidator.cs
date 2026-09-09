using Api.Request.Task;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.List
{
    public class UserTaskListValidator : AbstractValidator<UserTaskListRequest>
    {
        public UserTaskListValidator() 
        {
            RuleFor(x => x.PageNumber)
                .ApplyPageNumberRules();

            RuleFor(x => x.PageSize)
                .ApplyPageSizeRules();
        }
    }
}
