namespace Api.Request.Offer
{
    public record ExtendOfferValidityRequest
    {
        public Guid OfferId { get; init; }
        public DateTime? NewValidUntil { get; init; }
    }
}
