namespace DTO.Response
{
    public record TaskDetailResponse
    {
        public required Guid Id { get; init; }
        public required string Title { get; init; }
        public required DateTime DueAt { get; init; }
        public required string Status { get; init; }
        public required string Priority { get; init; }
    }
}
