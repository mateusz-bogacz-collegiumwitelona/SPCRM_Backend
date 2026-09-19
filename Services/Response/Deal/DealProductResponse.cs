namespace Services.Response.Deal
{
    public record DealProductResponse
    {
        public required Guid DealProductId { get; init; }
        public required Guid ProductId { get; init; }
        public required string Name { get; init; }
        public required string SteelGrade { get; init; }
        public required string Dimensions { get; init; }

        public required int Quantity { get; init; }
        public required string UnitSymbol { get; init; }

        public required long BaseUnitPrice { get; init; }
        public required long UnitPrice { get; init; }
        public required long TotalPrice { get; init; }

        public required string CurrencyCode { get; init; }
        public required int DecimalPlaces { get; init; }
    }
}
