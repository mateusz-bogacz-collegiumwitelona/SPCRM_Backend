using Domain.Common;
using Domain.Enum;
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

        public async Task<Result<object>> GetTaskDictionariesAsync()
        {
            var statuses = GetStatusDictionary();
            var priorities = GetPriorityDictionary();

            return Result<object>.Success(
                message: "Dictionaries retrieved successfully",
                data: new { Statuses = statuses, Priorities = priorities },
                statusCode: StatusCodes.Status200OK
                );
        }

        public async Task<Result<TaskDetailResponse>> GetTaskDetailResponse(Guid taskId)
        {
            var query = await _context.Tasks
                .AsNoTracking()
                .Where(t => t.Id == taskId)
                .Select(t => new TaskDetailResponse
                {
                    Id = t.Id,
                    Title = t.Title,
                    DueAt = t.DueAt,
                    Status = t.Status.ToString(),
                    Priority = t.Priority.ToString()
                })
                .FirstOrDefaultAsync();

            if (query == null)
            {
                return Result<TaskDetailResponse>.Failure(
                    message: "Task not found",
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            return Result<TaskDetailResponse>.Success(
                message: "Tasks detail retrieved successfully",
                data: query,
                statusCode: StatusCodes.Status200OK
                );
        }

        public async Task<Result<TaskContactResponse>> GetTaskContactAsync(Guid taskId)
        {
            var query = await _context.Tasks
                .AsNoTracking()
                .Where(t => t.Id == taskId && t.ContactId != null)
                .Select(t => new TaskContactResponse
                {
                    ContactId = t.Contact!.Id,
                    FirstName = t.Contact.FirstName,
                    LastName = t.Contact.LastName,
                    JobTitle = t.Contact.JobTitle ?? string.Empty,
                    CompanyName = t.Contact.Company.Name,

                    ContactWays = t.Contact.ContactDetails
                        .Where(cd => !cd.IsDeleted)
                        .Select(cd => new ContactWayResponse
                        {
                            Type = cd.Type.ToString(),
                            Value = cd.Value,
                            Label = cd.Label ?? string.Empty,
                            IsPrimary = cd.IsPrimary
                        }).ToList()
                })
                .FirstOrDefaultAsync();

            if (query == null)
            {
                return Result<TaskContactResponse>.Failure(
                    message: "Contact for this task not found",
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            return Result<TaskContactResponse>.Success(
                data: query,
                message: "Task contact card retrieved successfully",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result<TaskDealResponse>> GetTaskDealAsync(Guid taskId)
        {
            var query = await _context.Tasks
                .AsNoTracking()
                .Where(t => t.Id == taskId && t.DealId != null)
                .Select(t => new TaskDealResponse
                {
                    DealId = t.Deal!.Id,
                    Name = t.Deal.Name,
                    Value = t.Deal.Value,
                    Status = t.Deal.Status.ToString(),
                    CloseDate = t.Deal.CloseDate,
                    CurrencyCode = t.Deal.Currency.Code,
                    DecimalPlaces = t.Deal.Currency.DecimalPlaces
                })
                .FirstOrDefaultAsync();

            if (query == null)
            {
                return Result<TaskDealResponse>.Failure(
                    message: "Deal for this task not found",
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            return Result<TaskDealResponse>.Success(
                data: query,
                message: "Task deal card retrieved successfully",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result<List<TaskNoteResponse>>> GetTaskNotesAsync(Guid taskId)
        {
            bool isTaskExists = await _context.Tasks
                .AsNoTracking()
                .AnyAsync(t => t.Id == taskId);

            if (!isTaskExists)
            {
                return Result<List<TaskNoteResponse>>.Failure(
                    message: "Task for this note not found",
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            var query = await _context.Tasks
                .AsNoTracking()
                .Where(t => t.Id == taskId)
                .SelectMany(t => t.Notes)
                .Where(n => !n.IsDeleted)
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new TaskNoteResponse
                {
                    NoteId = n.Id,
                    Title = n.Title,
                    Content = n.Content,
                    AuthorFirstName = n.Author.FirstName,
                    AuthorLastName = n.Author.LastName,
                    CreatedAt = n.CreatedAt,
                    UpdatedAt = n.UpdateAt ?? null,
                })
                .ToListAsync();

            return Result<List<TaskNoteResponse>>.Success(
                data: query,
                message: "Task notes retrieved successfully",
                statusCode: StatusCodes.Status200OK
            );
        }

        private List<object> GetStatusDictionary()
            => new List<object>
                {
                    new { Value = TaskStatusEnum.ToDo.ToString(), Label = "Do zrobienia" },
                    new { Value = TaskStatusEnum.InProgress.ToString(), Label = "W trakcie" },
                    new { Value = TaskStatusEnum.Complete.ToString(), Label = "Zakończone" },
                    new { Value = TaskStatusEnum.Break.ToString(), Label = "Wstrzymane" }
                };

        private List<object> GetPriorityDictionary()
            => new List<object>
                {
                    new { Value = TaskPriorityEnum.Low.ToString(), Label = "Niski" },
                    new { Value = TaskPriorityEnum.Medium.ToString(), Label = "Średni" },
                    new { Value = TaskPriorityEnum.High.ToString(), Label = "Wysoki" }
                };
    }
}
