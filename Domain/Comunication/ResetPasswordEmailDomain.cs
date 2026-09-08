namespace Domain.Comunication
{
    public record ResetPasswordEmailDomain
    {
        public required Guid UserId { get; init; }
        public required string Email { get; init; }
        public required string UserName { get; init; }
        public required string Token { get; init; }
    }
}
