namespace Api.Request.Offer
{
    public record OfferProductItemRequest
    {
        public Guid ProductId { get; init; }
        public int Quantity { get; init; }
        public long QuotedPrice { get; init; } // x10000
    }
}
