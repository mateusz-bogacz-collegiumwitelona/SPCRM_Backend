using Api.Request.Deal;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Deal
{
    public class AddDealNoteValidator : AbstractValidator<AddDealNoteRequest>
    {
        public AddDealNoteValidator()
        {
            RuleFor(x => x.Title).ApplyTitleRules();
            RuleFor(x => x.Content).ApplyContentRules();
        }
    }
}
