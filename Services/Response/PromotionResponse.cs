namespace Services.Response
{
    public record PromotionResponse
    {
        public required Guid Id { get; init; }
        public required string Name { get; init; }
        public decimal? DiscountPercentage { get; init; }
        public long? PromotionalPrice { get; init; }
        public DateTime? StartDate { get; init; }
        public DateTime? EndDate { get; init; }
        public bool IsActive { get; init; }
    }
}
