using Api.Request.Invoice;
using Domain.Constants;
using FluentValidation;

namespace Api.Validators.Validators.Invoice
{
    public class AddInvoicePaymentValidator : AbstractValidator<AddInvoicePaymentRequest>
    {
        public AddInvoicePaymentValidator()
        {
            RuleFor(x => x.Amount)
                .GreaterThan(0)
                .WithErrorCode(ErrorCodes.InvoicePaymentInvalid);

            RuleFor(x => x.PaymentDate)
                .NotEmpty()
                .WithErrorCode(ErrorCodes.PaymentDateRequired)
                .LessThanOrEqualTo(_ => DateTime.UtcNow.AddMinutes(5))
                .WithErrorCode(ErrorCodes.InvalidDate)
                .GreaterThan(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc))
                .WithErrorCode(ErrorCodes.InvalidDate);

            RuleFor(x => x.ReferenceNumber)
                .MaximumLength(100)
                .WithErrorCode(ErrorCodes.PaymentReferenceNumberInvalid);

            RuleFor(x => x.Note)
                .MaximumLength(500)
                .WithErrorCode(ErrorCodes.PaymentNoteInvalid);
        }
    }
}
