namespace Api.Request
{
    public class NoteEditRequest
    {
        public required Guid Id { get; init; }

        public string? Title { get; init; }
        public string? Content { get; init; }
    }
}
