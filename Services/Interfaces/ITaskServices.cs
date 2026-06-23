using Domain.Common;
using DTO.Request;
using DTO.Response;

namespace Services.Interfaces
{
    public interface ITaskServices
    {
        Task<Result<List<TaskCalendarResponse>>> GetTasksForCalendarAsync(Guid userId, TaskCalendarRequest request);
        Task<Result<object>> GetTaskDictionariesAsync();
        Task<Result<TaskDetailResponse>> GetTaskDetailResponse(Guid taskId);
        Task<Result<TaskContactResponse>> GetTaskContactAsync(Guid taskId);
        Task<Result<TaskDealResponse>> GetTaskDealAsync(Guid taskId);
        Task<Result<List<NoteResponse>>> GetTaskNotesAsync(Guid taskId);
    }
}
