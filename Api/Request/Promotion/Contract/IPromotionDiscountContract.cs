namespace Api.Request.Promotion.Contract
{
    public interface IPromotionDiscountContract
    {
        decimal? DiscountPercentage { get; }
        long? PromotionalPrice { get; }
    }
}
