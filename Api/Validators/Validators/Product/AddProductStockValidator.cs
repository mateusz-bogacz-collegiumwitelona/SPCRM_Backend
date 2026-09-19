using Api.Request.Product;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Product
{
    public class AddProductStockValidator : AbstractValidator<AddProductStockRequest>
    {
        public AddProductStockValidator()
        {
            RuleFor(x => x.QuantityToAdd)
                .ApplyProductStockQuantityRule();
        }
    }
}
