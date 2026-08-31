namespace Services.Response
{
    public record OfferListResponse
    {
        public required Guid OfferId { get; init; }
        public required string OfferName { get; init; }
        public required string ContactFirstName { get; init; }
        public required string ContactLastName { get; init; }
        public required string CompanyName { get; init; }
        public required DateTime ValidUntil { get; init; } 
        public required string Status { get; init; }
    }
}
