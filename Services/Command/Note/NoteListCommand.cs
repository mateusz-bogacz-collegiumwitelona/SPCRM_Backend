using Services.Command.List;

namespace Services.Command.Note
{
    public record NoteListCommand : SimpleListCommand
    {
        public required Guid SearchId { get; init; }
    }
}
