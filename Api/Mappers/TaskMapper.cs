using Api.Request.Task;
using Domain.Enum;
using Riok.Mapperly.Abstractions;
using Services.Command.Task;

namespace Api.Mappers
{
    [Mapper]
    public partial class TaskMapper
    {
        public TaskCalendarCommand MapUserCalendar(Guid userId, TaskCalendarRequest request)
            => new TaskCalendarCommand
            {
                UserId = userId,
                DateFrom = request.DateFrom,
                DateTo = request.DateTo,
                TaskPriority = ParseTaskPriority(request.TaskPriority),
                TaskStatus = ParseTaskStatus(request.TaskStatus)
            };

        public UserTaskListCommand MapUserTask(Guid userId, UserTaskListRequest request)
            => new UserTaskListCommand
            {
                UserId = userId,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                SearchTerm = request.SearchTerm,
                Status = ParseTaskStatus(request.Status),
                Priority = ParseTaskPriority(request.Priority),
                SortBy = request.SortBy,
                SortDescending = request.SortDescending
            };

        [MapProperty(nameof(SalesTaskListRequest.Status), nameof(SalesTaskListCommand.Status), Use = nameof(ParseTaskStatus))]
        [MapProperty(nameof(SalesTaskListRequest.Priority), nameof(SalesTaskListCommand.Priority), Use = nameof(ParseTaskPriority))]
        public partial SalesTaskListCommand MapSaleTasks(SalesTaskListRequest request);

        private TaskStatusEnum? ParseTaskStatus(string? status)
            => Enum.TryParse<TaskStatusEnum>(status, true, out var parsed) ? parsed : null;

        private TaskPriorityEnum? ParseTaskPriority(string? priority)
            => Enum.TryParse<TaskPriorityEnum>(priority, true, out var parsed) ? parsed : null;
    }
}
