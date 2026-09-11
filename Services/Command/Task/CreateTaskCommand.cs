using Domain.Enum;

namespace Services.Command.Task
{
    public record CreateTaskCommand
    {
        public required string Title { get; init; }
        public required string Description { get; init; }
        public required DateTime DueAt { get; init; }
        public required TaskPriorityEnum Priority { get; init; }
        public Guid? AssignedToId { get; init; }
        public Guid? TargetId { get; init; }
        public TaskTargetTypeEnum TargetType { get; init; } = TaskTargetTypeEnum.None;
    }
}
