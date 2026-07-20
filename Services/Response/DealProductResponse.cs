namespace Services.Response
{
    public record DealProductResponse
    {
        public required Guid ProductId { get; init; }
        public required string Name { get; init; }
        public required string SteelGrade { get; init; }
        public required string Dimensions { get; init; }

        public required int Quantity { get; init; }
        public required string UnitSymbol { get; init; }

        public required long BaseUnitPrice { get; init; } // prze rabatem
        public required long UnitPrice { get; init; }     // po rabacie
        public required long TotalPrice { get; init; }    // netto

        public required string CurrencyCode { get; init; }
        public required int DecimalPlaces { get; init; }
    }
}
