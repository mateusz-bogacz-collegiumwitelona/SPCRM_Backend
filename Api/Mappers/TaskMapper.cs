using Api.Request.Task;
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
                TaskPriority = request.TaskPriority,
                TaskStatus = request.TaskStatus
            };
    }
}
