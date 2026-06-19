using Domain.Common;
using DTO.Request;
using DTO.Response;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Helpers;
using Services.Interfaces;

namespace Services.Services
{
    public class TaskServices : ITaskServices
    {
        private readonly AppDbContext _context;
        private readonly ILogger<TaskServices> _logger;

        public TaskServices(AppDbContext context, ILogger<TaskServices> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result<List<TaskCalendarResponse>>> GetTasksForCalendarAsync(Guid userId, TaskCalendarRequest request)
        {
            var fromUtc = request.DateFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var toUtc = request.DateTo.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

            var query = await _context.Tasks
                .AsNoTracking()
                .Where(t => !t.IsDeleted)
                .Where(t => t.AssignedToId == userId)
                .Where(t => t.DueAt >= fromUtc && t.DueAt <= toUtc)
                .OrderBy(t => t.DueAt)
                .ApplyFilterByStatus(request.TaskStatus ?? string.Empty)
                .ApplyFilterByPriority(request.TaskPriority ?? string.Empty)
                .Select(t => new TaskCalendarResponse
                {
                    Id = t.Id,
                    Title = t.Title,
                    DueAt = t.DueAt,
                    Status = t.Status.ToString(),
                    Priority = t.Priority.ToString(),
                    ContactFirstName = t.Contact != null ? t.Contact.FirstName : string.Empty,
                    ContactLastName = t.Contact != null ? t.Contact.LastName : string.Empty,
                    ContactId = t.Contact != null ? t.Contact.Id : null,
                    DealName = t.Deal != null ? t.Deal.Name : string.Empty,
                    DealId = t.Deal != null ? t.Deal.Id : null
                })
                .ToListAsync();

            return Result<List<TaskCalendarResponse>>.Success(
                message: "Tasks retrieved successfully",
                data: query,
                statusCode: StatusCodes.Status200OK
                );
        }
    }
}
