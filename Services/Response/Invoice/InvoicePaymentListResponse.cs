namespace Services.Response.Invoice
{
    public record InvoicePaymentListResponse
    {
        public required Guid PaymentId { get; init; }
        public required long Amount { get; init; }
        public required string CurrencyCode { get; init; }
        public required int DecimalPlaces { get; init; }
        public required DateTime PaymentDate { get; init; }
        public string? ReferenceNumber { get; init; }
        public string? Note { get; init; }
        public string? CreatedByFirstName { get; init; }
        public string? CreatedByLastName { get; init; }
    }
}
