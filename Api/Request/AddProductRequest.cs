namespace Api.Request
{
    public record AddProductRequest
    {
        public required string Name { get; init; }
        public required string SteelGrade { get; init; }
        public int Thickness { get; init; }
        public int Width { get; init; }
        public int Length { get; init; }
        public int? Diameter { get; init; }
        public decimal Weight { get; init; }
        public required Guid UnitId { get; init; }
        public decimal PricePerUnit { get; init; }
        public int StockQuantity { get; init; }
        public required string Category { get; init; }
    }
}
