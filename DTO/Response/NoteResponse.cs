namespace DTO.Response
{
    public record NoteResponse
    {
        public required Guid NoteId { get; set; }
        public required String Title { get; init; }
        public required String Content { get; init; }
        public required string AuthorFirstName { get; init; }
        public required string AuthorLastName { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
    }
}
