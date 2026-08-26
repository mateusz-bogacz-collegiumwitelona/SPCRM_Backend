using Api.Request;
using Domain.Constants;
using FluentValidation;

namespace Api.Validators
{
    public class DeleteSteelGradeRequestValidator : AbstractValidator<DeleteSteelGradeRequest>
    {
        public DeleteSteelGradeRequestValidator()
        {
            RuleForEach(x => x.Reassignments)
                .SetValidator(new ProductReassignmentRequestValidator());

            RuleFor(x => x.Reassignments)
                .Must(reassignments => reassignments == null ||
                                       reassignments.Select(r => r.ProductId).Distinct().Count() == reassignments.Count)
                .WithErrorCode(ErrorCodes.DuplicateProductReassignment)
                .When(x => x.Reassignments != null && x.Reassignments.Count > 1);
        }
    }
}
