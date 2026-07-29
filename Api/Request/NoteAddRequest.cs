using Domain.Common;

namespace Api.Request
{
    public record NoteAddRequest
    {
        public required Guid TargetId { get; init; }
        public required string Title { get; init; }
        public required string Content { get; init; }
        public required NoteEnum NoteType { get; init; }
    }
}
