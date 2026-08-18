namespace Services.Response
{
    public record OwnerResponse
    {
        public required Guid Id { get; init; }
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public required string Role { get; init; }
    }
}
