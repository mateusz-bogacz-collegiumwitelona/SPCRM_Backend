using Api.Request;
using Riok.Mapperly.Abstractions;
using Services.Command;

namespace Api.Mappers
{
    [Mapper]
    public partial class TaskMapper
    {
        public TaskCalendarCommand MapUserCalendar(Guid userId, TaskCalendarRequest request)
        {
            return new TaskCalendarCommand
            {
                UserId = userId,
                DateFrom = request.DateFrom,
                DateTo = request.DateTo,
                TaskPriority = request.TaskPriority,
                TaskStatus = request.TaskStatus
            };
        }
    }
}
