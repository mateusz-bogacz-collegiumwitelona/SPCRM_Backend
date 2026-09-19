namespace Services.Command.Product
{
    public record ProductDealItemResponse
    {
        public required Guid DealId { get; init; }
        public required string DealName { get; init; }
        public required string CompanyName { get; init; }
        public required string Status { get; init; }
        public required int Quantity { get; init; }
        public required long UnitPrice { get; init; }
        public required long TotalPrice { get; init; }
        public required string CurrencyCode { get; init; }
        public required int DecimalPlaces { get; init; }
        public required DateTime CloseDate { get; init; }
    }
}
