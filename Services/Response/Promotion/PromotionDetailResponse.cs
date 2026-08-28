namespace Services.Response.Promotion
{
    public record PromotionDetailResponse
    {
        public required Guid Id { get; init; }
        public required string Name { get; init; }
        public bool IsActive { get; init; }
        public DateTime? StartDate { get; init; }
        public DateTime? EndDate { get; init; }
        public decimal? DiscountPercentage { get; init; }
        public long? PromotionalPrice { get; init; }
        public string? CurrencyCode { get; init; }
        public int? CurrencyDecimalPlaces { get; init; }
        public int? MinQuantity { get; init; }
        public int? MinWeight { get; init; }
        public required Guid ProductId { get; init; }
        public required string ProductName { get; init; }
        public required string SteelGrade { get; init; }
        public required string Category { get; init; }
        public required string Dimensions { get; init; }
        public long ProductPricePerUnit { get; init; }
        public int ProductStockQuantity { get; init; }
        public required string UnitSymbol { get; init; }
        public Guid? ContactId { get; init; }
        public string? ContactFirstName { get; init; }
        public string? ContactLastName { get; init; }
        public string? ContactCompanyName { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdateAt { get; init; }
    }
}
