namespace Api.Request.Note
{
    public record NoteEditRequest
    {
        public required Guid Id { get; init; }

        public string? Title { get; init; }
        public string? Content { get; init; }
    }
}
