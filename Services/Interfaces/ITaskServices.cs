using Domain.Common;
using Services.Command;
using Services.Response;

namespace Services.Interfaces
{
    public interface ITaskServices
    {
        Task<Result<List<TaskCalendarResponse>>> GetTasksForCalendarAsync(TaskCalendarCommand command);
        Task<Result<object>> GetTaskDictionariesAsync();
        Task<Result<TaskDetailResponse>> GetTaskDetailResponse(Guid taskId);
        Task<Result<TaskContactResponse>> GetTaskContactAsync(Guid taskId);
        Task<Result<TaskDealResponse>> GetTaskDealAsync(Guid taskId);
        Task<Result<List<NoteResponse>>> GetTaskNotesAsync(Guid taskId);
    }
}
