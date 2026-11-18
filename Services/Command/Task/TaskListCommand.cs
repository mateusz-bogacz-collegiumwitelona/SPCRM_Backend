using Domain.Enum;
using Services.Command.List;

namespace Services.Command.Task
{
    public record TaskListCommand : BasicListCommand
    {
        public TaskStatusEnum? Status { get; init; }
        public TaskPriorityEnum? Priority { get; init; }
    }
}
