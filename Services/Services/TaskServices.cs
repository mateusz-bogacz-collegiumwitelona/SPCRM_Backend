using Domain.Common;
using Domain.Constants;
using Domain.Enum;
using Domain.Exceptions.Exception;
using Domain.Models;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Command.Task;
using Services.Factory.Interfaces;
using Services.Helpers;
using Services.Interfaces;
using Services.QueryExtension;
using Services.Response.Contact;
using Services.Response.Task;

namespace Services.Services
{
    public class TaskServices : ITaskServices
    {
        private readonly AppDbContext _context;
        private readonly ILogger<TaskServices> _logger;
        private readonly IEntityAuthorizationService _entityAuth;

        private readonly ITaskStateMachineFactory _state;

        public TaskServices(
            AppDbContext context,
            ILogger<TaskServices> logger,
            IEntityAuthorizationService entityAuth,
            ITaskStateMachineFactory state)
        {
            _context = context;
            _logger = logger;
            _entityAuth = entityAuth;
            _state = state;
        }

        public async Task<Result<List<TaskCalendarResponse>>> GetTasksForCalendarAsync(TaskCalendarCommand command)
        {
            var fromUtc = command.DateFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var toUtc = command.DateTo.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

            var query = await _context.Tasks
                .AsNoTracking()
                .Where(t => t.AssignedToId == command.UserId)
                .Where(t => t.DueAt >= fromUtc && t.DueAt <= toUtc)
                .OrderBy(t => t.DueAt)
                .ApplyFilter(command.TaskStatus, command.TaskPriority)
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
                    Description = t.Description,
                    DueAt = t.DueAt,
                    Status = t.Status.ToString(),
                    Priority = t.Priority.ToString()
                })
                .FirstOrDefaultAsync();

            if (query == null)
            {
                _logger.LogInformation("Task with ID {TaskId} not found.", taskId);
                return Result<TaskDetailResponse>.Failure(
                    message: "Task not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.TaskNotFound
                );
            }

            if (string.IsNullOrWhiteSpace(query.Title))
            {
                _logger.LogError("Critical data corruption: Task {TaskId} has empty title.", taskId);
                throw new DataCorruptionException($"Task '{taskId}' contains corrupted state.");
            }

            return Result<TaskDetailResponse>.Success(
                message: "Tasks detail retrieved successfully",
                data: query,
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result<TaskContactResponse>> GetTaskContactAsync(Guid taskId)
        {
            var query = await (
                from t in _context.Tasks.AsNoTracking()
                where t.Id == taskId && t.ContactId != null
                join c in _context.Contacts.AsNoTracking() on t.ContactId equals c.Id into cGroup
                from c in cGroup.DefaultIfEmpty()
                join comp in _context.Companies.AsNoTracking() on c.CompanyId equals comp.Id into compGroup
                from comp in compGroup.DefaultIfEmpty()
                select new
                {
                    HasContact = c != null,
                    ContactId = c != null ? c.Id : Guid.Empty,
                    FirstName = c != null ? c.FirstName : null,
                    LastName = c != null ? c.LastName : null,
                    JobTitle = c != null ? c.JobTitle : null,
                    CompanyName = comp != null ? comp.Name : null,
                    ContactWays = c != null ? c.ContactDetails
                        .Where(cd => !cd.IsDeleted)
                        .Select(cd => new ContactWayResponse
                        {
                            Type = cd.Type.ToString(),
                            Value = cd.Value,
                            Label = cd.Label ?? string.Empty,
                            IsPrimary = cd.IsPrimary
                        }).ToList() : new List<ContactWayResponse>()
                }
            ).FirstOrDefaultAsync();

            if (query == null || !query.HasContact)
            {
                _logger.LogInformation("Contact for task with ID {TaskId} not found.", taskId);
                return Result<TaskContactResponse>.Failure(
                    message: "Contact for this task not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.ContactNotFound
                );
            }

            if (string.IsNullOrWhiteSpace(query.FirstName) || string.IsNullOrWhiteSpace(query.CompanyName))
            {
                _logger.LogError("Critical data corruption: Contact linked to Task {TaskId} has missing required fields.", taskId);
                throw new DataCorruptionException($"Contact linked to task '{taskId}' contains corrupted state.");
            }

            var response = new TaskContactResponse
            {
                ContactId = query.ContactId,
                FirstName = query.FirstName,
                LastName = query.LastName ?? string.Empty,
                JobTitle = query.JobTitle ?? string.Empty,
                CompanyName = query.CompanyName,
                ContactWays = query.ContactWays
            };

            return Result<TaskContactResponse>.Success(
                data: response,
                message: "Task contact card retrieved successfully",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result<TaskDealResponse>> GetTaskDealAsync(Guid taskId)
        {
            var query = await (
                from t in _context.Tasks.AsNoTracking()
                where t.Id == taskId && t.DealId != null
                join d in _context.Deals.AsNoTracking() on t.DealId equals d.Id into dGroup
                from d in dGroup.DefaultIfEmpty()
                join curr in _context.Currencies.AsNoTracking() on d.CurrencyId equals curr.Id into currGroup
                from curr in currGroup.DefaultIfEmpty()
                select new
                {
                    HasDeal = d != null,
                    DealId = d != null ? d.Id : Guid.Empty,
                    Name = d != null ? d.Name : null,
                    Value = d != null ? d.Value : 0,
                    Status = d != null ? d.Status.ToString() : null,
                    CloseDate = d != null ? d.CloseDate : default,
                    CurrencyCode = curr != null ? curr.Code : null,
                    DecimalPlaces = curr != null ? (int?)curr.DecimalPlaces : null
                }
            ).FirstOrDefaultAsync();

            if (query == null || !query.HasDeal)
            {
                _logger.LogInformation("Deal for task with ID {TaskId} not found.", taskId);
                return Result<TaskDealResponse>.Failure(
                    message: "Deal for this task not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.DealNotFound
                );
            }

            if (query.Value < 0 || string.IsNullOrWhiteSpace(query.CurrencyCode) || !query.DecimalPlaces.HasValue)
            {
                _logger.LogError("Critical data corruption: Deal linked to Task {TaskId} has invalid value or missing currency linkage.", taskId);
                throw new DataCorruptionException($"Deal linked to task '{taskId}' contains corrupted financial or currency data.");
            }

            var response = new TaskDealResponse
            {
                DealId = query.DealId,
                Name = query.Name ?? string.Empty,
                Value = query.Value,
                Status = query.Status ?? string.Empty,
                CloseDate = query.CloseDate,
                CurrencyCode = query.CurrencyCode,
                DecimalPlaces = query.DecimalPlaces.Value
            };

            return Result<TaskDealResponse>.Success(
                data: response,
                message: "Task deal card retrieved successfully",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result<PagedResult<UserTaskResponse>>> GetUserTasksAsync(UserTaskListCommand command)
            => await _context.Tasks
                    .AsNoTracking()
                    .Where(t => t.AssignedToId == command.UserId)
                    .ApplySearch(command.SearchTerm ?? string.Empty)
                    .ApplySorting(command.SortBy ?? string.Empty, command.SortDescending)
                    .ApplyFilter(command.Status, command.Priority)
                    .Select(t => new UserTaskResponse
                    {
                        Id = t.Id,
                        Title = t.Title,
                        DueAt = t.DueAt,
                        Status = t.Status.ToString(),
                        Priority = t.Priority.ToString(),
                        ContactName = t.Contact != null ? $"{t.Contact.FirstName} {t.Contact.LastName}".Trim() : null,
                        DealName = t.Deal != null ? t.Deal.Name : null
                    })
                    .ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "user-tasks");

        public async Task<Result<PagedResult<SaleTaskResponse>>> GetDealTasksAsync(
            Guid dealId,
            SalesTaskListCommand command,
            Guid currentUserId)
        {
            var dealOwnerId = await _context.Deals
                .AsNoTracking()
                .Where(d => d.Id == dealId)
                .Select(d => (Guid?)d.OwnerId)
                .FirstOrDefaultAsync();

            if (!dealOwnerId.HasValue)
            {
                _logger.LogInformation("Deal {DealId} not found when retrieving tasks.", dealId);
                return Result<PagedResult<SaleTaskResponse>>.Failure(
                    message: "Sale not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.DealNotFound
                );
            }

            var hasAccess = await _entityAuth.CanModifyAsync(currentUserId, dealOwnerId.Value);
            if (!hasAccess)
            {
                _logger.LogWarning("User {UserId} unauthorized access to tasks of Deal {DealId}.", currentUserId, dealId);
                throw new ForbiddenException("You do not have permission to view tasks for this deal.");
            }

            return await _context.Tasks
                .AsNoTracking()
                .Where(t => t.DealId == dealId)
                .ApplySearch(command.SearchTerm ?? string.Empty)
                .ApplyFilter(command.Status, command.Priority)
                .ApplySorting(command.SortBy, command.SortDescending)
                .Select(t => new SaleTaskResponse
                {
                    Id = t.Id,
                    Title = t.Title,
                    DueAt = t.DueAt,
                    Status = t.Status.ToString(),
                    Priority = t.Priority.ToString(),
                    AssignedToId = t.AssignedToId,
                    AssignedToFirstName = t.AssignedTo.FirstName,
                    AssignedToLastName = t.AssignedTo.LastName,
                    ContactId = t.ContactId,
                    ContactFirstName = t.Contact != null ? t.Contact.FirstName : null,
                    ContactLastName = t.Contact != null ? t.Contact.LastName : null
                })
                .ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "deal-tasks");
        }

        public async Task<Result> AddTaskAsync(AddTaskCommand command, Guid userId)
        {
            var targetAssigneeId = command.AssignedToId ?? userId;

            if (targetAssigneeId != userId)
            {
                var isManager = await _entityAuth.CanAccessAsync(userId);
                if (!isManager)
                {
                    _logger.LogWarning("User {UserId} attempted to assign task to user {TargetAssigneeId} without permissions.", userId, targetAssigneeId);
                    throw new ForbiddenException("You do not have permission to assign tasks to another user.");
                }
            }

            var requiredUserIds = targetAssigneeId == userId
                ? new[] { userId }
                : new[] { userId, targetAssigneeId };

            var existingUserIds = await _context.Users
                .AsNoTracking()
                .Where(u => requiredUserIds.Contains(u.Id) && !u.IsDeleted)
                .Select(u => u.Id)
                .ToListAsync();

            if (!existingUserIds.Contains(userId))
            {
                _logger.LogInformation("Creator user with ID {UserId} not found or deleted.", userId);
                return Result.Failure(
                    message: "User not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.UserNotFound
                );
            }

            if (!existingUserIds.Contains(targetAssigneeId))
            {
                _logger.LogInformation("Assigned user with ID {AssignedToId} not found or deleted.", targetAssigneeId);
                return Result.Failure(
                    message: "Assigned user not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.UserNotFound
                );
            }

            Guid? dealId = null;
            Guid? contactId = null;

            if (command.TargetId.HasValue && command.TargetType != TaskTargetTypeEnum.None)
            {
                switch (command.TargetType)
                {
                    case TaskTargetTypeEnum.Deal:
                        var dealExists = await _context.Deals.AnyAsync(d => d.Id == command.TargetId.Value);
                        if (!dealExists)
                        {
                            _logger.LogInformation("Deal with ID {DealId} not found for task.", command.TargetId.Value);
                            return Result.Failure(
                                message: "Deal for this task not found.",
                                statusCode: StatusCodes.Status404NotFound,
                                errorCode: ErrorCodes.DealNotFound
                            );
                        }
                        dealId = command.TargetId.Value;
                        break;

                    case TaskTargetTypeEnum.Contact:
                        var contactExists = await _context.Contacts.AnyAsync(c => c.Id == command.TargetId.Value);
                        if (!contactExists)
                        {
                            _logger.LogInformation("Contact with ID {ContactId} not found for task.", command.TargetId.Value);
                            return Result.Failure(
                                message: "Contact for this task not found.",
                                statusCode: StatusCodes.Status404NotFound,
                                errorCode: ErrorCodes.ContactNotFound
                            );
                        }
                        contactId = command.TargetId.Value;
                        break;
                }
            }

            var task = new Tasks
            {
                Title = command.Title,
                Description = command.Description,
                DueAt = DateTime.SpecifyKind(command.DueAt, DateTimeKind.Utc),
                Priority = command.Priority,
                Status = TaskStatusEnum.ToDo,
                AssignedToId = targetAssigneeId,
                CreatedById = userId,
                DealId = dealId,
                ContactId = contactId
            };

            _context.Tasks.Add(task);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Task {TaskId} created successfully by {CreatorId} and assigned to {AssignedToId}.", task.Id, userId, targetAssigneeId);

            return Result.Success(
                message: "Task created successfully.",
                statusCode: StatusCodes.Status201Created
            );
        }

        public async Task<Result> DeleteTaskAsync(Guid taskId, Guid userId)
        {
            var task = await _context.Tasks.FindAsync(taskId);

            if (task == null)
            {
                _logger.LogInformation("Task with ID {TaskId} not found.", taskId);
                return Result.Failure(
                    message: "Task not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.TaskNotFound
                );
            }

            var isManager = await _entityAuth.CanAccessAsync(userId);
            var isSelfOwnedTask = task.CreatedById == userId && task.AssignedToId == userId;

            if (!isManager && !isSelfOwnedTask)
            {
                _logger.LogWarning("User {UserId} unauthorized attempt to delete Task {TaskId} created by {CreatedById}.", userId, taskId, task.CreatedById);
                throw new ForbiddenException("You do not have permission to delete this task.");
            }

            var stateMachine = _state.Create(task);
            var canModify = stateMachine.CanModify();

            if (!canModify.IsSuccess)
            {
                _logger.LogWarning("Task {TaskId} cannot be deleted due to its current state.", taskId);
                return canModify;
            }

            _context.Tasks.Remove(task);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Task {TaskId} deleted successfully by user {UserId}.", taskId, userId);

            return Result.Success(
                message: "Task deleted successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result> EditTaskAsync(EditTaskCommand command)
        {
            var task = await _context.Tasks.FindAsync(command.TaskId);

            if (task == null)
            {
                _logger.LogInformation("Task with ID {TaskId} not found.", command.TaskId);
                return Result.Failure(
                    message: "Task not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.TaskNotFound
                );
            }

            var isManager = await _entityAuth.CanAccessAsync(command.UserId);
            var isSelfOwnedTask = task.CreatedById == command.UserId && task.AssignedToId == command.UserId;

            if (!isManager && !isSelfOwnedTask)
            {
                _logger.LogWarning("User {UserId} unauthorized attempt to edit Task {TaskId} created by {CreatedById}.", command.UserId, command.TaskId, task.CreatedById);
                throw new ForbiddenException("You do not have permission to edit this task.");
            }

            var stateMachine = _state.Create(task);
            var canModify = stateMachine.CanModify();

            if (!canModify.IsSuccess)
            {
                _logger.LogWarning("Task {TaskId} cannot be edited due to its current state.", command.TaskId);
                return canModify;
            }

            if (!string.IsNullOrWhiteSpace(command.Title))
            {
                task.Title = command.Title.Trim();
            }

            if (!string.IsNullOrWhiteSpace(command.Description))
            {
                task.Description = command.Description.Trim();
            }

            if (command.Priority.HasValue)
            {
                task.Priority = command.Priority.Value;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Task {TaskId} updated successfully by user {UserId}.", task.Id, command.UserId);

            return Result.Success(
                message: "Task updated successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result> ExtendTaskDueDateAsync(ExtendTaskDueDateCommand command)
        {
            var task = await _context.Tasks.FindAsync(command.TaskId);

            if (task == null)
            {
                _logger.LogInformation("Task with ID {TaskId} not found.", command.TaskId);
                return Result.Failure(
                    message: "Task not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.TaskNotFound
                );
            }

            var isManager = await _entityAuth.CanAccessAsync(command.UserId);
            var isSelfOwnedTask = task.CreatedById == command.UserId && task.AssignedToId == command.UserId;

            if (!isManager && !isSelfOwnedTask)
            {
                _logger.LogWarning("User {UserId} unauthorized attempt to extend due date for Task {TaskId} created by {CreatedById}.", command.UserId, command.TaskId, task.CreatedById);
                throw new ForbiddenException("You do not have permission to change the due date for this task.");
            }

            var stateMachine = _state.Create(task);
            var canModify = stateMachine.CanModify();

            if (!canModify.IsSuccess)
            {
                _logger.LogWarning("Due date for Task {TaskId} cannot be modified due to its current state.", command.TaskId);
                return canModify;
            }

            var utcNewDueDate = DateTime.SpecifyKind(command.NewDueDate, DateTimeKind.Utc);

            if (utcNewDueDate <= task.DueAt)
            {
                _logger.LogWarning("Attempted to set NewDueDate {NewDueDate} earlier or equal to current DueAt {CurrentDueAt} for task {TaskId}.", utcNewDueDate, task.DueAt, command.TaskId);
                return Result.Failure(
                    message: "New due date must be later than the current due date.",
                    errorCode: ErrorCodes.InvalidDate,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            task.DueAt = utcNewDueDate;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Task {TaskId} due date extended successfully to {NewDueDate} by user {UserId}.", task.Id, task.DueAt, command.UserId);

            return Result.Success(
                message: "Task due date extended successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result> ChangeAssignedToUserAsync(Guid taskId, Guid newAssignedToUserId, Guid managerId)
        {
            var task = await _context.Tasks.FindAsync(taskId);

            if (task == null)
            {
                _logger.LogInformation("Task with ID {TaskId} not found.", taskId);
                return Result.Failure(
                    message: "Task not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.TaskNotFound
                );
            }

            var stateMachine = _state.Create(task);
            var canModify = stateMachine.CanModify();

            if (!canModify.IsSuccess)
            {
                _logger.LogWarning("Task {TaskId} assignee cannot be changed due to its current state.", taskId);
                return canModify;
            }

            var newUser = await _context.Users.FindAsync(newAssignedToUserId);

            if (newUser == null || newUser.IsDeleted)
            {
                _logger.LogInformation("User with ID {NewAssignedToUserId} not found or deleted.", newAssignedToUserId);
                return Result.Failure(
                    message: "Assigned user not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.UserNotFound
                );
            }

            if (task.AssignedToId == newAssignedToUserId)
            {
                _logger.LogInformation("Task {TaskId} is already assigned to user {NewAssignedToUserId}.", taskId, newAssignedToUserId);
                return Result.Failure(
                    message: "Task is already assigned to this user.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.TaskAlreadyAssigned
                );
            }

            task.AssignedToId = newUser.Id;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Task {TaskId} reassigned successfully to user {NewAssignedToUserId} by manager {ManagerId}.", task.Id, newUser.Id, managerId);

            return Result.Success(
                message: "Task reassigned successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result> ChangeTaskStatusAsync(ChangeTaskStatusCommand command)
        {
            var task = await _context.Tasks.FindAsync(command.TaskId);

            if (task == null)
            {
                _logger.LogInformation("Task with ID {TaskId} not found.", command.TaskId);
                return Result.Failure(
                    message: "Task not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.TaskNotFound
                );
            }

            if (task.AssignedToId != command.UserId)
            {
                _logger.LogInformation("User {UserId} is not the assignee of task {TaskId}.", command.UserId, command.TaskId);
                return Result.Failure(
                    message: "User is not the owner of this task.",
                    statusCode: StatusCodes.Status403Forbidden,
                    errorCode: ErrorCodes.UserNotOwnThisTask
                );
            }

            var stateMachine = _state.Create(task);

            var canModify = stateMachine.CanModify();
            if (!canModify.IsSuccess)
            {
                _logger.LogWarning("Task {TaskId} status cannot be modified due to its current state.", command.TaskId);
                return canModify;
            }

            var transitionResult = stateMachine.TransitionTo(command.Status);
            if (!transitionResult.IsSuccess)
            {
                _logger.LogWarning("Invalid status transition for task {TaskId} to {TargetStatus}.", command.TaskId, command.Status);
                return transitionResult;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Task {TaskId} status changed successfully to {NewStatus} by user {UserId}.", task.Id, command.Status, command.UserId);

            return Result.Success(
                message: "Task status changed successfully.",
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
