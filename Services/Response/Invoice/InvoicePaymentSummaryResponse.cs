using System;
using System.Collections.Generic;
using System.Text;

namespace Services.Response.Invoice
{
    public record InvoicePaymentSummaryResponse
    {
        public required Guid InvoiceId { get; init; }
        public required string InvoiceNumber { get; init; }
        public required long TotalAmount { get; init; }
        public required long PaidAmount { get; init; }
        public required long RemainingAmount { get; init; }
        public required string CurrencyCode { get; init; }
        public required int DecimalPlaces { get; init; }
        public required DateTime DueDate { get; init; }
        public DateTime? PaymentDate { get; init; }
        public required bool IsOverDue { get; init; }
        public required int PaymentsCount { get; init; }
    }
}
