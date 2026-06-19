using Domain.Common;
using DTO.Request;
using DTO.Response;

namespace Services.Interfaces
{
    public interface ITaskServices
    {
        Task<Result<List<TaskCalendarResponse>>> GetTasksForCalendarAsync(Guid userId, TaskCalendarRequest request);
    }
}
