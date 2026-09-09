using Domain.Enum;

namespace Services.Command.Task
{
    public record UserTaskListCommand
    {
        public required Guid UserId { get; init; }
        public int? PageNumber { get; init; }
        public int? PageSize { get; init; }
        public string? SearchTerm { get; init; }
        public TaskStatusEnum? Status { get; init; }
        public TaskPriorityEnum? Priority { get; init; }
        public string? SortBy { get; init; }
        public bool SortDescending { get; init; } = false;
    }
}
