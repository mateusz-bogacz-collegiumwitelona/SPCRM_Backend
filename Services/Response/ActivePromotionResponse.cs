namespace Services.Response
{
    public record ActivePromotionResponse
    {
        public string Name { get; init; } = string.Empty;
        public decimal? DiscountPercentage { get; init; }
        public decimal? PromotionalPrice { get; init; }
        public DateTime? EndDate { get; init; }
        public int? MinQuantity { get; init; }
    }
}
