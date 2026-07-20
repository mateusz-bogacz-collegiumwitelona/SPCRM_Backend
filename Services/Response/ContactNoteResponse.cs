
namespace Services.Response
{
    public record ContactNoteResponse
    {
        public Guid Id { get; init; }
        public required String Title { get; init; }
        public required String Content { get; init; }
        public required string AuthorFirstName { get; init; }
        public required string AuthorLastName { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdateAt { get; init; }
    }
}
