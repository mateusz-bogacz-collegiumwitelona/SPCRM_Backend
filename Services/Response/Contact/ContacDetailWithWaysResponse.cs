namespace Services.Response.Contact
{
    public record ContacDetailWithWaysResponse
    {
        public required Guid ContactId { get; init; }
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public string? JobTitle { get; init; }
        public required List<ContactWayDetailResponse> Details { get; init; }
    }
}
