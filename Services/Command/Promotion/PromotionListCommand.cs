using Services.Command.List;

namespace Services.Command.Promotion
{
    public record PromotionListCommand : BasicListCommand
    {
        public bool? IsActive { get; init; }
        public DateTime? FromDate { get; init; }
        public DateTime? ToDate { get; init; }
        public decimal? DiscountPrecentageFrom { get; init; }
        public decimal? DiscountPrecentageTo { get; init; }
        public long? PromotionPriceFrom { get; init; }
        public long? PromotionPriceTo { get; init; }
    }
}
