namespace Services.Response.Contact
{
    public record ContactDealResponse
    {
        public required Guid ContactId { get; init; }
        public required string ContactFirstName { get; init; }
        public required string ContactLastName { get; init; }
        public required bool IsPrimary { get; init; }
        public required Guid CompanyId { get; init; }
        public required string CompanyName { get; init; }
        public required string Nip { get; init; }
    }
}
