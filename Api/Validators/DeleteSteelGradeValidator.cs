using Api.Request;
using Domain.Constants;
using FluentValidation;

namespace Api.Validators
{
    public class DeleteSteelGradeValidator : AbstractValidator<DeleteSteelGradeRequest>
    {
        public DeleteSteelGradeValidator()
        {
            RuleForEach(x => x.Reassignments)
                .SetValidator(new ProductReassignmentValidator());

            RuleFor(x => x.Reassignments)
                .Must(reassignments => reassignments == null ||
                                       reassignments.Select(r => r.ProductId).Distinct().Count() == reassignments.Count)
                .WithErrorCode(ErrorCodes.DuplicateProductReassignment)
                .When(x => x.Reassignments != null && x.Reassignments.Count > 1);
        }
    }
}
