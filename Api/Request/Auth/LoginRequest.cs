namespace Api.Request.Auth
{
    public record LoginRequest
    {
        public required string Name { get; init; }
        public required string Password { get; init; }
        public bool RememberMe { get; init; }
    }
}
