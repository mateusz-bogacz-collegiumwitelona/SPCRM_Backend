namespace Services.Response.Offer
{
    public record OfferClientDetail
    {
        public required Guid ContactId { get; init; }
        public required string ContactFirstName { get; init; }
        public required string ContactLastName { get; init; }
        public string? ContactJobTitle { get; init; }
        public required string CompanyName { get; init; }
    }
}
