using Domain.Enum;

namespace Services.Command.Task
{
    public record EditTaskCommand
    {
        public required Guid TaskId { get; init; }
        public required Guid UserId { get; init; }
        public string? Title { get; init; }
        public string? Description { get; init; }
        public TaskPriorityEnum? Priority { get; init; }
    }
}
