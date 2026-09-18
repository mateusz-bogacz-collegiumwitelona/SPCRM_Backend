namespace Services.Response.Invoice
{
    public record InvoiceProductsListResponse
    {
        public required Guid InvoiceProductId { get; init; }
        public required string ProductName { get; init; }
        public string? SteelGrade { get; init; }
        public required string UnitSymbol { get; init; }
        public required int Quantity { get; init; }
        public required long UnitPrice { get; init; }
        public required long TotalPrice { get; init; }
    }
}
