using Domain.Constants;
using Domain.Enum;
using FluentValidation;

namespace Api.Validators.Rule
{
    public static class TaskValidationRules
    {
        public static IRuleBuilderOptions<T, string?> ApplyTaskTitleRules<T>(this IRuleBuilder<T, string?> ruleBuilder)
            => ruleBuilder
                .NotEmpty().WithErrorCode(ErrorCodes.InvalidOperation)
                .MaximumLength(150).WithErrorCode(ErrorCodes.InvalidOperation);

        public static IRuleBuilderOptions<T, string?> ApplyTaskDescriptionRules<T>(this IRuleBuilder<T, string?> ruleBuilder)
            => ruleBuilder
                .NotEmpty().WithErrorCode(ErrorCodes.InvalidOperation)
                .MaximumLength(1000).WithErrorCode(ErrorCodes.InvalidOperation);

        public static IRuleBuilderOptions<T, DateTime> ApplyTaskDueAtRules<T>(this IRuleBuilder<T, DateTime> ruleBuilder)
            => ruleBuilder
                .NotEmpty().WithErrorCode(ErrorCodes.InvalidDate)
                .GreaterThan(DateTime.UtcNow.AddMinutes(-5)).WithErrorCode(ErrorCodes.InvalidDate);

        public static IRuleBuilderOptions<T, string?> ApplyTaskPriorityRules<T>(this IRuleBuilder<T, string?> ruleBuilder)
            => ruleBuilder
                .NotEmpty().WithErrorCode(ErrorCodes.InvalidOperation)
                .IsEnumName(typeof(TaskPriorityEnum), caseSensitive: false)
                .WithErrorCode(ErrorCodes.InvalidOperation);

        public static IRuleBuilderOptions<T, string> ApplyTaskStatusRules<T>(this IRuleBuilder<T, string> ruleBuilder)
            => ruleBuilder
                .NotEmpty().WithErrorCode(ErrorCodes.InvalidOperation)
                .IsEnumName(typeof(TaskStatusEnum), caseSensitive: false)
                .WithErrorCode(ErrorCodes.InvalidOperation);
    }
}

