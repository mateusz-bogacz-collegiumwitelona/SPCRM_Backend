using Api.Request.Contract;

namespace Api.Request
{
    public record EditProductRequest : IEditProductDimensionsContract
    {
        public required Guid ProductId { get; init; }
        public string? Name { get; init; }
        public Guid? SteelGradeId { get; init; }
        public decimal? Thickness { get; init; }
        public decimal? Width { get; init; }
        public decimal? Length { get; init; }
        public decimal? Diameter { get; init; }
        public decimal? Weight { get; init; }
        public Guid? UnitId { get; init; }
        public decimal? PricePerUnit { get; init; }
        public Guid? CurrencyId { get; init; }
        public int? StockQuantity { get; init; }
        public string? Category { get; init; }
    }
}
