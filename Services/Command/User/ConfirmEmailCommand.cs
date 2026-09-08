namespace Services.Command.User
{
    public record ConfirmEmailCommand
    {
        public required string Email { get; init; }
        public required string Token { get; init; }
        public required string Password { get; init; }

    }
}
