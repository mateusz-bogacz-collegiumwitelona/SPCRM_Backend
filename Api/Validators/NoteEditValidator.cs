using Api.Request;
using Domain.Constants;
using FluentValidation;

namespace Api.Validators
{
    public class NoteEditValidator : AbstractValidator<NoteEditRequest>
    {
        public NoteEditValidator()
        {
            RuleFor(x => x.Id)
                .NotEmpty()
                .WithErrorCode(ErrorCodes.NoteIdRequired)
                .NotEqual(Guid.Empty)
                .WithErrorCode(ErrorCodes.NoteIdRequired);

            RuleFor(x => x.Title)
                .Length(1, 50)
                .WithErrorCode(ErrorCodes.NoteTitleIsNotValid);

            RuleFor(x => x.Content)
                .Length(1, 500)
                .WithErrorCode(ErrorCodes.NoteContentIsNotValid);
        }
    }
}
