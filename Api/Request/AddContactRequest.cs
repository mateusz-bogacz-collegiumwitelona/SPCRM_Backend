namespace Api.Request
{
    public record AddContactRequest
    {
        public required Guid CompanyId { get; init; }
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public string? JobTitle { get; init; }
        public required List<AddContactDetailRequest> Details { get; init; }
    }
}
