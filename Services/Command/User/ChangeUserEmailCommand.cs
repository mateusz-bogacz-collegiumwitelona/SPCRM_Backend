namespace Services.Command.User
{
    public record ChangeUserEmailCommand
    {
        public required Guid UserId { get; init; }
        public required string NewEmail { get; init; }
    }
}
