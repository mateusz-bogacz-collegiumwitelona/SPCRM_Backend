using Api.Request.Invoice;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Invoice
{
    public class InvoiceListValidator : AbstractValidator<InvoiceListRequest>
    {
        public InvoiceListValidator()
        {
            RuleFor(x => x.PageNumber)
                .ApplyPageNumberRules();

            RuleFor(x => x.PageSize)
                .ApplyPageSizeRules();
        }
    }
}
