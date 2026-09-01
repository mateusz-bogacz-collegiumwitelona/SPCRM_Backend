namespace Api.Request.Offer
{
    public record ResendOfferEmailRequest
    {
        public Guid OfferId { get; init; }
        public string? Language { get; init; }
    }
}
