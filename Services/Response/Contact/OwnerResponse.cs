namespace Services.Response.Contact
{
    public record OwnerResponse
    {
        public required Guid Id { get; init; }
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public required string Role { get; init; }
    }
}
