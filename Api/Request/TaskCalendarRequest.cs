namespace Api.Request
{
    public record TaskCalendarRequest
    {
        public required DateOnly DateFrom { get; init; }
        public required DateOnly DateTo { get; init; }

        public string? TaskPriority { get; init; }
        public string? TaskStatus { get; init; }
    }
}
