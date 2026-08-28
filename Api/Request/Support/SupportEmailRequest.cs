namespace Api.Request.Support
{
    public record SupportEmailRequest
    {
        public required string Email { get; init; }
        public required string Title { get; init; }
        public required string Message { get; init; }
    }
}
