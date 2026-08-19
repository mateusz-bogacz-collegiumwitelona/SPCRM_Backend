namespace Services.Command
{
    public record EditPromotionCommand
    {
        public required Guid Id { get; init; }
        public string? Name { get; init; }
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
