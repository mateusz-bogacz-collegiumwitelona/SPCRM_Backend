namespace Services.Command.Support
{
    public record SupportEmailCommand
    {
        public required string Email { get; init; }
        public required string Title { get; init; }
        public required string Message { get; init; }
    }
}
