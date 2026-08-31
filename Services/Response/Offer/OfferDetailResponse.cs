namespace Services.Response.Offer
{
    public record OfferDetailResponse
    {
        public required Guid OfferId { get; init; }
        public required string OfferName { get; init; }
        public required string Status { get; init; }
        public required DateTime ValidUntil { get; init; }
        public bool IsExpired { get; init; }
        public string? CreatedByUserFirstName { get; init; }
        public string? CreatedByUserLastName { get; init; }
    }
}
