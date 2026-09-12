using Domain.Common;
using Services.Command.Task;
using Services.Response.Task;

namespace Services.Interfaces
{
    public interface ITaskServices
    {
        Task<Result<List<TaskCalendarResponse>>> GetTasksForCalendarAsync(TaskCalendarCommand command);
        Task<Result<object>> GetTaskDictionariesAsync();
        Task<Result<TaskDetailResponse>> GetTaskDetailResponse(Guid taskId);
        Task<Result<TaskContactResponse>> GetTaskContactAsync(Guid taskId);
        Task<Result<TaskDealResponse>> GetTaskDealAsync(Guid taskId);
        Task<Result<PagedResult<UserTaskResponse>>> GetUserTasksAsync(UserTaskListCommand command);
        Task<Result<PagedResult<SaleTaskResponse>>> GetDealTasksAsync(Guid dealId, SalesTaskListCommand command, Guid currentUserId);
        Task<Result> AddTaskAsync(AddTaskCommand command, Guid userId);
        Task<Result> DeleteTaskAsync(Guid taskId, Guid userId);
        Task<Result> EditTaskAsync(EditTaskCommand command);
        Task<Result> ExtendTaskDueDateAsync(ExtendTaskDueDateCommand command);
        Task<Result> ChangeAssignedToUserAsync(Guid taskId, Guid newAssignedToUserId, Guid managerId);
    }
}
