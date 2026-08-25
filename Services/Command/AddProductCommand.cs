namespace Services.Command
{
    public record AddProductCommand
    {
        public required string Name { get; init; }
        public required Guid SteelGradeId { get; init; }
        public required int Thickness { get; init; }
        public required int Width { get; init; }
        public required int Length { get; init; }
        public int? Diameter { get; init; }
        public required int Weight { get; init; } // kg * 1000
        public required Guid UnitId { get; init; }
        public long PricePerUnit { get; init; }
        public required Guid CurrencyId { get; init; }
        public int StockQuantity { get; init; }
        public required string Category { get; init; }
    }
}
