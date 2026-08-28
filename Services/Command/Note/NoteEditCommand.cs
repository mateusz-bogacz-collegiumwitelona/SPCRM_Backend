namespace Services.Command.Note
{
    public record NoteEditCommand
    {
        public required Guid Id { get; init; }

        public string? Title { get; init; }
        public string? Content { get; init; }
    }
}
