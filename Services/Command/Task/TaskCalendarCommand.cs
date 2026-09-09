using Domain.Enum;

namespace Services.Command.Task
{
    public record TaskCalendarCommand
    {
        public required Guid UserId { get; init; }
        public required DateOnly DateFrom { get; init; }
        public required DateOnly DateTo { get; init; }
        public TaskPriorityEnum? TaskPriority { get; init; }
        public TaskStatusEnum? TaskStatus { get; init; }
    }
}
