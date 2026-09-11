using Domain.Enum;

namespace Api.Request.Note
{
    public record NoteAddRequest
    {
        public required Guid TargetId { get; init; }
        public required string Title { get; init; }
        public required string Content { get; init; }
        public required NoteTypeEnum NoteType { get; init; }
    }
}
