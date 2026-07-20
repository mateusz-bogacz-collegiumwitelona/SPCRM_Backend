namespace Services.Response
{
    public record TaskCalendarResponse
    {
        public required Guid Id { get; init; }
        public required string Title { get; init; }
        public required DateTime DueAt { get; init; }
        public required string Status { get; init; }
        public required string Priority { get; init; }
        public string? ContactFirstName { get; init; }
        public string? ContactLastName { get; init; }
        public Guid? ContactId { get; init; }
        public string? DealName { get; init; }
        public Guid? DealId { get; init; }
    }
}
