namespace Api.Request.User
{
    public record EditUserRequest
    {
        public required Guid UserId { get; init; }
        public string? FirstName { get; init; }
        public string? LastName { get; init; }
        public string? Email { get; init; }
    }
}
