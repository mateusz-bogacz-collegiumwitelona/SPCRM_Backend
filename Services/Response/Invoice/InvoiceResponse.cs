namespace Services.Response.Invoice
{
    public record InvoiceResponse
    {
        public required Guid Id { get; init; }
        public required string InvoiceNumber { get; init; }
        public required long TotalAmount { get; init; } // x10000
        public required long PaidAmount { get; init; } // x10000
        public required DateTime IssueDate { get; init; }
        public required DateTime DueDate { get; init; }
        public DateTime? PaymentDate { get; init; }
        public required string CurrencyCode { get; init; }
        public required int DecimalPlaces { get; init; }
        public required string CompanyName { get; init; }
        public required string CompanyNip { get; init; }
        public long RemainingAmount { get; init; }
        public bool IsOverDue { get; init; }
    }
}
