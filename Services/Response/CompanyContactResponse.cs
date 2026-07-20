namespace Services.Response
{
    public record CompanyContactResponse
    {
        public required Guid Id { get; init; }
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public string? JobTitle { get; init; }
        public required bool IsPrimary { get; init; }
        public string? OwnerFirstName { get; init; }
        public string? OwnerLastName { get; init; }
    }
}
