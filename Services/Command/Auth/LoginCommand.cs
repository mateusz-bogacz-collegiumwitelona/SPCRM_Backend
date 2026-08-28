namespace Services.Command.Auth
{
    public record LoginCommand
    {
        public required string Name { get; init; }
        public required string Password { get; init; }
    }
}
