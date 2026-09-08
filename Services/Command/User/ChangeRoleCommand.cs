namespace Services.Command.User
{
    public record ChangeRoleCommand
    {
        public required Guid UserId { get; init; }
        public required string Role { get; init; }
    }
}
