namespace Services.Command.Auth
{
    public record ResetPasswordCommand
    {
        public required Guid UserId { get; init; }
        public required string Token { get; init; }
        public required string Password { get; init; }
    }
}
