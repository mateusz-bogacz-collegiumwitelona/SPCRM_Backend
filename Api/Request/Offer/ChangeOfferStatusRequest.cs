namespace Api.Request.Offer
{
    public record ChangeOfferStatusRequest
    {
        public Guid OfferId { get; init; }
        public required string NewStatus { get; init; }
    }
}
