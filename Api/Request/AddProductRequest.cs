namespace Api.Request
{
    public record AddProductRequest
    {
        public required string Name { get; init; }
        public required string SteelGrade { get; init; }
        public required int Thickness { get; init; }
        public required int Width { get; init; }
        public required int Length { get; init; }
        public int? Diameter { get; init; }
        public required int Weight { get; init; }
        public required Guid UnitId { get; init; }
        public long PricePerUnit { get; init; }
        public int StockQuantity { get; init; }
        public required string Category { get; init; }
    }
}
