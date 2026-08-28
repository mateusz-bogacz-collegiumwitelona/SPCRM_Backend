using Api.Request.SteelGrade;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Product
{
    public class ProductReassignmentValidator : AbstractValidator<ProductReassignmentRequest>
    {
        public ProductReassignmentValidator()
        {
            RuleFor(x => x.ProductId)
                .ApplyValidGuidRule();

            RuleFor(x => x.NewSteelGradeId)
                .ApplyValidGuidRule();
        }
    }
}
