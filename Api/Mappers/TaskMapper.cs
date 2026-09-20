using Api.Mappers.Helper;
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

        [MapProperty(nameof(TaskListRequest.Status), nameof(TaskListCommand.Status), Use = nameof(ParseTaskStatus))]
        [MapProperty(nameof(TaskListRequest.Priority), nameof(TaskListCommand.Priority), Use = nameof(ParseTaskPriority))]
        public partial TaskListCommand MapList(TaskListRequest request);

        public AddTaskCommand MapAddTaskToDeal(AddTaskRequest request, Guid dealId)
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

        public AddTaskCommand MapAddTaskToContact(AddTaskRequest request, Guid contactId)
            => new AddTaskCommand
            {
                Title = NormalizeName(request.Title),
                Description = Trim(request.Description),
                DueAt = request.DueAt,
                Priority = ParseTaskPriorityNotNull(request.Priority),
                AssignedToId = request.AssignedToId,
                TargetId = contactId,
                TargetType = TaskTargetTypeEnum.Contact
            };

        public EditTaskCommand MapEdit(EditTaskRequest request, Guid taskId, Guid userId)
            => new EditTaskCommand
            {
                TaskId = taskId,
                UserId = userId,
                Title = request.Title != null ? NormalizeName(request.Title) : null,
                Description = request.Description != null ? Trim(request.Description) : null,
                Priority = ParseTaskPriority(request.Priority)
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

        private TaskStatusEnum ParseTaskStatusNotNull(string status)
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
