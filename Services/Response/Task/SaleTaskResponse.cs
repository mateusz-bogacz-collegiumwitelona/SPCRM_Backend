namespace Services.Response.Task
{
    public record SaleTaskResponse
    {
        public required Guid Id { get; init; }
        public required string Title { get; init; }
        public required DateTime DueAt { get; init; }
        public required string Status { get; init; }
        public required string Priority { get; init; }
        public required Guid AssignedToId { get; init; }
        public required string AssignedToFirstName { get; init; }
        public required string AssignedToLastName { get; init; }
        public Guid? ContactId { get; init; }
        public string? ContactFirstName { get; init; }
        public string? ContactLastName { get; init; }
    }
}
