namespace Services.Response.Task
{
    public record UserTaskResponse
    {
        public required Guid Id { get; init; }
        public required string Title { get; init; }
        public required DateTime DueAt { get; init; }
        public required string Status { get; init; }
        public required string Priority { get; init; }
        public string? ContactName { get; init; }
        public string? DealName { get; init; }
    }
}
