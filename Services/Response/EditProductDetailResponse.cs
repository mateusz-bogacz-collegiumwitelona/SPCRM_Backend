namespace Services.Response
{
    public record EditProductDetailResponse
    {
        public required Guid ProductId { get; init; }
        public required string Name { get; init; }

        public required Guid SteelGradeId { get; init; }
        public required Guid UnitId { get; init; }
        public required Guid CurrencyId { get; init; }
        public required string Category { get; init; }

        public decimal Thickness { get; init; }
        public decimal Width { get; init; }
        public decimal Length { get; init; }
        public decimal? Diameter { get; init; }
        public decimal Weight { get; init; }
        public decimal PricePerUnit { get; init; }
        public int StockQuantity { get; init; }
    }
}
