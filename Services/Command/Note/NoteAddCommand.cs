using Domain.Common;

namespace Services.Command.Note
{
    public record NoteAddCommand
    {
        public required Guid AuthorId { get; init; }
        public required Guid TargetId { get; init; }
        public required string Title { get; init; }
        public required string Content { get; init; }
        public required NoteEnum NoteType { get; init; }
    }
}
