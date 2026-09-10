using Domain.Enum;

namespace Services.Command.Task
{
    public record SalesTaskListCommand
    {
        public int? PageNumber { get; init; }
        public int? PageSize { get; init; }
        public string? SearchTerm { get; init; }
        public string? SortBy { get; init; }
        public bool SortDescending { get; init; } = false;
        public TaskStatusEnum? Status { get; init; }
        public TaskPriorityEnum? Priority { get; init; }
    }
}
