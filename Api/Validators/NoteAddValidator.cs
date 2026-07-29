using Api.Request;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators
{
    public class NoteAddValidator : AbstractValidator<NoteAddRequest>
    {
        public NoteAddValidator()
        {
            RuleFor(x => x.TargetId).ApplyNoteIdRules();
            RuleFor(x => x.Title).ApplyTitleRules();
            RuleFor(x => x.Content).ApplyContentRules();
        }
    }
}
