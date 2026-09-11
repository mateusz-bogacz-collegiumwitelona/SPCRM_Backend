namespace Api.Request.Deal
{
    public record AddDealTaskRequest
    {
        public required string Title { get; init; }
        public required string Description { get; init; }
        public required DateTime DueAt { get; init; }
        public required string Priority { get; init; }
        public Guid? AssignedToId { get; init; }
    }
}
