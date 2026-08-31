using Api.Request.Note;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Note
{
    public class NoteEditValidator : AbstractValidator<NoteEditRequest>
    {
        public NoteEditValidator()
        {
            RuleFor(x => x.Id).ApplyNoteIdRules();
            RuleFor(x => x.Title).ApplyTitleRules();
            RuleFor(x => x.Content).ApplyContentRules();
        }
    }
}
