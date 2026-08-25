namespace Services.Command
{
    public record EditProductCommand
    {
        public required Guid ProductId { get; init; }
        public string? Name { get; init; }
        public Guid? SteelGradeId { get; init; }
        public int? Thickness { get; init; }
        public int? Width { get; init; }
        public int? Length { get; init; }
        public int? Diameter { get; init; }
        public int? Weight { get; init; } // kg * 1000
        public Guid? UnitId { get; init; }
        public long? PricePerUnit { get; init; }
        public int? StockQuantity { get; init; }
        public string?  Category { get; init; }
    }
}
