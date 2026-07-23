namespace Services.Response
{
    public record ProductDetailResponse
    {
        public required Guid Id { get; init; }
        public required string Name { get; init; }
        public required string SteelGrade { get; init; }
        public required string Category { get; init; }
        public required string Dimensions { get; init; }
        public required int StockQuantity { get; init; }
        public required string UnitSymbol { get; init; }
        public required decimal PricePerUnit { get; init; }
        public required decimal Weight { get; init; }
        public required int ReservedQuantity { get; init; }
    }
}
