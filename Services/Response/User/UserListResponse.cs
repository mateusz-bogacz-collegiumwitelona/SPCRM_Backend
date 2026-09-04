namespace Services.Response.User
{
    public record UserListResponse
    {
        public required Guid Id { get; init; }
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public required string Role { get; init; }
        public required bool IsBlocked { get; init; }
    }
}
