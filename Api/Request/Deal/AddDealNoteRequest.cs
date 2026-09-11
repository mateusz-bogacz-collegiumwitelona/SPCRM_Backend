namespace Api.Request.Deal
{
    public record AddDealNoteRequest
    {
        public required string Title { get; init; }
        public required string Content { get; init; }
    }
}
