using Api.Mappers.Helper;
using Api.Request.Deal;
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

        public AddTaskCommand MapAddTaskToDeal(AddDealTaskRequest request, Guid dealId)
            => new AddTaskCommand
            {
                Title = NormalizeName(request.Title),
                Description = Trim(request.Description),
                DueAt = request.DueAt,
                Priority = ParseTaskPriorityNotNull(request.Priority),
                AssignedToId = request.AssignedToId,
                TargetId = dealId,
                TargetType = TaskTargetTypeEnum.Deal
            };

        public EditTaskCommand MapEdit(EditTaskRequest request, Guid taskId, Guid userId)
            => new EditTaskCommand
            {
                TaskId = taskId,
                UserId = userId,
                Title = request.Title != null ? NormalizeName(request.Title) : null,
                Description = request.Description != null ? Trim(request.Description) : null
            };

        public ExtendTaskDueDateCommand MapExtendDueDate(ExtendTaskDueDateRequest request, Guid taskId, Guid userId)
            => new ExtendTaskDueDateCommand
            {
                TaskId = taskId,
                UserId = userId,
                NewDueDate = request.NewDueDate
            };

        public ChangeTaskStatusCommand MapChangeStatus(ChangeTaskStatusRequest request, Guid userId)
            => new ChangeTaskStatusCommand
            {
                TaskId = request.TaskId,
                UserId = userId,
                Status = ParseTaskStatusNotNull(request.Status)
            };

        private TaskStatusEnum? ParseTaskStatus(string? status)
            => Enum.TryParse<TaskStatusEnum>(status, true, out var parsed) ? parsed : null;

        private TaskStatusEnum ParseTaskStatusNotNull(string  status)
            => Enum.TryParse<TaskStatusEnum>(status, true, out var parsed) ? parsed : (TaskStatusEnum)(-1);

        private TaskPriorityEnum? ParseTaskPriority(string? priority)
            => Enum.TryParse<TaskPriorityEnum>(priority, true, out var parsed) ? parsed : null;

        private TaskPriorityEnum ParseTaskPriorityNotNull(string priority)
            => Enum.TryParse<TaskPriorityEnum>(priority, true, out var parsed) ? parsed : (TaskPriorityEnum)(-1);

        private string NormalizeName(string? value)
            => StringNormalizerHelper.NormalizeName(value) ?? string.Empty;

        public string Trim(string? value)
            => StringNormalizerHelper.Trim(value) ?? string.Empty;
    }
}
