namespace Services.Command
{
    public record TaskCalendarCommand
    {
        public required Guid UserId { get; init; }
        public required DateOnly DateFrom { get; init; }
        public required DateOnly DateTo { get; init; }
        public string? TaskPriority { get; init; }
        public string? TaskStatus { get; init; }
    }
}
