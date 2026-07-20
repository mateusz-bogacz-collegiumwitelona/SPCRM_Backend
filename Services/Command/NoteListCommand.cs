namespace Services.Command
{
    public record NoteListCommand
    {
        public required Guid searchId { get; init; }
        public int? PageNumber { get; init; }
        public int? PageSize { get; init; }
        public string? SearchTerm { get; init; }

    }
}
