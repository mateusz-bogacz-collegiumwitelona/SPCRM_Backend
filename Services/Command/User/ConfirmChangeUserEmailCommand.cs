namespace Services.Command.User
{
    public record ConfirmChangeUserEmailCommand
    {
        public required Guid UserId { get; init; }
        public required string Token { get; init; }
    }
}
