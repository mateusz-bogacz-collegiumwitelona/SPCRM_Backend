namespace DTO.Response
{
    public record ContactsResponse
    {
        public required Guid Id { get; init; }
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public required string CompanyName { get; init; }
        public string? OwnerFirstName { get; init; }
        public string? OwnerLastName { get; init; }
        public required bool IsPrimary { get; init; }
    }
}
