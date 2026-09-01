namespace Api.Request.Offer
{
    public record UpdateOfferProductsRequest
    {
        public Guid OfferId { get; init; }
        public List<OfferProductItemRequest> Items { get; init; } = new();
    }
}
