namespace Services.Command.User
{
    public record SetLockoutCommand
    {
        public required Guid UserId { get; init; }
        public DateTime? LockoutEnd { get; init; }
    }
}
