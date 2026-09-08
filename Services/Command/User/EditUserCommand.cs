namespace Services.Command.User
{
    public record EditUserCommand
    {
        public required Guid UserId { get; init; }
        public string? FirstName { get; init; }
        public string? LastName { get; init; }
    }
}
