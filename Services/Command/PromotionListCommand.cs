namespace Services.Command
{
    public record PromotionListCommand
    {
        public int? PageNumber { get; init; }
        public int? PageSize { get; init; }
        public bool? IsActive { get; init; } = true;
        public DateTime? FromDate { get; init; }
        public DateTime? ToDate { get; init; }
        public decimal? DiscountPrecentageFrom { get; init; }
        public decimal? DiscountPrecentageTo { get; init; }
        public long? PromotionPriceFrom { get; init; }
        public long? PromotionPriceTo { get; init; }
        public string? SearchTerm { get; init; }
        public string? SortBy { get; init; }
        public bool SortDescending { get; init; } = false;
    }
}
