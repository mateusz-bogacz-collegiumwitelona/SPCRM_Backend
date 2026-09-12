using Domain.Enum;

namespace Services.Command.Task
{
    public record ChangeTaskStatusCommand
    {
        public required Guid TaskId { get; init; }
        public required Guid UserId { get; init; }
        public required TaskStatusEnum Status { get; init; }
    }
}
