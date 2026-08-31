using Api.Request.Note;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Note
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
