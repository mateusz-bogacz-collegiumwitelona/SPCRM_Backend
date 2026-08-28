namespace Api.Request.Promotion
{
    public record AddPromotionRequest
    {
        public required string Name { get; init; }
        public required Guid ProductId { get; init; }
        public DateTime? StartDate { get; init; }
        public DateTime? EndDate { get; init; }
        public decimal? DiscountPercentage { get; init; }
        public long? PromotionalPrice { get; init; }
        public Guid? CurrencyId { get; init; }
        public Guid? ContactId { get; init; }
        public int? MinQuantity { get; init; }
        public int? MinWeight { get; init; }
    }
}
