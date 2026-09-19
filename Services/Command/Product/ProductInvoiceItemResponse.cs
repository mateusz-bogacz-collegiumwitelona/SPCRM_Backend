namespace Services.Command.Product
{
    public record ProductInvoiceItemResponse
    {
        public required Guid InvoiceId { get; init; }
        public required string InvoiceNumber { get; init; }
        public required string CompanyName { get; init; }
        public required DateTime IssueDate { get; init; }
        public required DateTime DueDate { get; init; }
        public required int Quantity { get; init; }
        public required long UnitPrice { get; init; }
        public required long TotalPrice { get; init; }
        public required string CurrencyCode { get; init; }
        public required int DecimalPlaces { get; init; }
        public required bool IsPaid { get; init; }
    }
}
