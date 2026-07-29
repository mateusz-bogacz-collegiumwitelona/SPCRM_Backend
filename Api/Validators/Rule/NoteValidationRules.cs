using Domain.Constants;
using FluentValidation;

namespace Api.Validators.Rule
{
    public static class NoteValidationRules
    {
        public static IRuleBuilderOptions<T, Guid> ApplyNoteIdRules<T>(this IRuleBuilder<T, Guid> ruleBuilder)
            => ruleBuilder
                .NotEmpty()
                .WithErrorCode(ErrorCodes.NoteIdRequired)
                .NotEqual(Guid.Empty)
                .WithErrorCode(ErrorCodes.NoteIdRequired);

        public static IRuleBuilderOptions<T, string?> ApplyTitleRules<T>(this IRuleBuilder<T, string?> ruleBuilder)
            => ruleBuilder
                .Length(1, 50)
                .WithErrorCode(ErrorCodes.NoteTitleIsNotValid);

        public static IRuleBuilderOptions<T, string?> ApplyContentRules<T>(this IRuleBuilder<T, string?> ruleBuilder)
            => ruleBuilder
                .Length(1, 500)
                .WithErrorCode(ErrorCodes.NoteContentIsNotValid);
    }
}
