namespace Services.Response
{
    public record TaskContactResponse
    {
        public required Guid ContactId { get; init; }
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public string? JobTitle { get; init; }
        public required string CompanyName { get; init; }

        public List<ContactWayResponse> ContactWays { get; init; } = new();
    }
}
