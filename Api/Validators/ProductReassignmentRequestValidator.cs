using Api.Request;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators
{
    public class ProductReassignmentRequestValidator : AbstractValidator<ProductReassignmentRequest>
    {
        public ProductReassignmentRequestValidator()
        {
            RuleFor(x => x.ProductId)
                .ApplyValidGuidRule();

            RuleFor(x => x.NewSteelGradeId)
                .ApplyValidGuidRule();
        }
    }
}
