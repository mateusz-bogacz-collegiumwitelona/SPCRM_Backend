namespace Services.Command.User
{
    public record DeleteUserCommand
    {
        public required Guid UserId { get; init; }
        public required Guid ReassignToUserId { get; init; }
    }
}
