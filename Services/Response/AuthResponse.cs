namespace Services.Response
{
    public record AuthResponse
    {
        public required string Token { get; init; }
        public required Guid UserId { get; init; }
        public required string Email { get; init; }
        public required string UserName { get; init; }
        public required IList<string> Roles { get; init; }
    }
}
