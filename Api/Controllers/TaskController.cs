using Api.Attributes;
using Api.Controllers.Base;
using Api.Mappers;
using Api.Request.Task;
using Domain.Common;
using Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Services.Interfaces;
using Services.Response.Note;
using Services.Response.Task;

namespace Api.Controllers
{
    [Route("api/tasks")]
    [ApiController]
    public class TaskController : BaseControlle
    {
        [EndpointSummary("Get tasks for calendar")]
        [EndpointDescription("Show tasks for calendar view. " +
            "This endpoint return tasks within a specified date range")]
        [ProducesResponseType(typeof(Result<List<TaskCalendarResponse>>), StatusCodes.Status200OK)]
        [HttpGet("calendar")]
        [Authorize(Roles = "User,Manager")]
        [OutputCache(PolicyName = "UserAuthPolicy", Tags = new string[] { CacheTags.TasksForCalendar })]
        public async Task<IActionResult> GetTasksForCalendarAsync(
            [FromServices] TaskMapper mapper,
            [FromServices] ITaskServices taskServices,
            [FromQuery] TaskCalendarRequest request
            )
        {
            var result = await taskServices.GetTasksForCalendarAsync(mapper.MapUserCalendar(CurrentUserId, request));
            return HandleResult(result);
        }

        [EndpointSummary("Get task dictionaries")]
        [EndpointDescription("Returns available statuses and priorities for frontend dropdowns.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet("dictionaries")]
        [Authorize(Roles = "User,Manager")]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.TaskDictionary })]
        public async Task<IActionResult> GetTaskDictionariesAsync([FromServices] ITaskServices taskServices)
        {
            var result = await taskServices.GetTaskDictionariesAsync();
            return HandleResult(result);
        }

        [EndpointSummary("Get task detail")]
        [EndpointDescription("Returns detailed information about a specific task.")]
        [ProducesResponseType(typeof(Result<TaskDetailResponse>), StatusCodes.Status200OK)]
        [HttpGet("{taskId:guid}")]
        [Authorize(Roles = "User,Manager")]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.TaskDetails })]
        public async Task<IActionResult> GetTaskDetailResponse(
            [FromServices] ITaskServices taskServices,
            [FromRoute] Guid taskId
            )
        {
            var result = await taskServices.GetTaskDetailResponse(taskId);
            return HandleResult(result);
        }

        [EndpointSummary("Get task contact")]
        [EndpointDescription("Returns contact information associated with a specific task.")]
        [ProducesResponseType(typeof(Result<TaskContactResponse>), StatusCodes.Status200OK)]
        [HttpGet("{taskId:guid}/contact")]
        [Authorize(Roles = "User,Manager")]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.TaskContacs })]
        public async Task<IActionResult> GetTaskContactAsync(
            [FromServices] ITaskServices taskServices,
            [FromRoute] Guid taskId
        )
        {
            var result = await taskServices.GetTaskContactAsync(taskId);
            return HandleResult(result);
        }

        [EndpointSummary("Get task deal")]
        [EndpointDescription("Returns deal information associated with a specific task.")]
        [ProducesResponseType(typeof(Result<TaskDealResponse>), StatusCodes.Status200OK)]
        [HttpGet("{taskId:guid}/deal")]
        [Authorize(Roles = "User,Manager")]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.TaskDeals })]
        public async Task<IActionResult> GetTaskDealAsync(
            [FromServices] ITaskServices taskServices,
            [FromRoute] Guid taskId
        )
        {
            var result = await taskServices.GetTaskDealAsync(taskId);
            return HandleResult(result);
        }

        [EndpointSummary("Get task notes")]
        [EndpointDescription("Returns notes associated with a specific task.")]
        [ProducesResponseType(typeof(Result<List<NoteResponse>>), StatusCodes.Status200OK)]
        [HttpGet("{taskId:guid}/notes")]
        [Authorize(Roles = "User,Manager")]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.TaskNotes })]
        public async Task<IActionResult> GetTaskNotesAsync(
            [FromServices] INoteServices note,
            [FromRoute] Guid taskId
        )
        {
            var result = await note.GetTaskNotesAsync(taskId);
            return HandleResult(result);
        }

        [EndpointSummary("Delete task")]
        [EndpointDescription("Delete a specific task.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpDelete("{taskId:guid}")]
        [Authorize(Roles = "User,Manager")]
        [InvalidateCache(nameof(CacheTags.TaskAll), nameof(CacheTags.AnalyticsAll), nameof(CacheTags.UserAll))]
        public async Task<IActionResult> DeleteTaskAsync(
            [FromServices] ITaskServices task,
            [FromRoute] Guid taskId
            )
        {
            var result = await task.DeleteTaskAsync(taskId, CurrentUserId);
            return HandleResult(result);
        }

        [EndpointSummary("Edit task")]
        [EndpointDescription("Edit a specific task.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpPut("{taskId:guid}")]
        [Authorize(Roles = "User,Manager")]
        [InvalidateCache(nameof(CacheTags.TaskAll), nameof(CacheTags.AnalyticsAll))]
        public async Task<IActionResult> EditTaskAsync(
            [FromServices] TaskMapper mapper,
            [FromServices] ITaskServices task,
            [FromRoute] Guid taskId,
            [FromBody] EditTaskRequest request
            )
        {
            var result = await task.EditTaskAsync(mapper.MapEdit(request, taskId, CurrentUserId));
            return HandleResult(result);
        }

        [EndpointSummary("Extend task due date")]
        [EndpointDescription("Extend the due date of a specific task.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpPut("{taskId:guid}/extend-due-date")]
        [Authorize(Roles = "User,Manager")]
        [InvalidateCache(nameof(CacheTags.TaskAll), nameof(CacheTags.AnalyticsAll))]
        public async Task<IActionResult> ExtendTaskDueDateAsync(
            [FromServices] TaskMapper mapper,
            [FromServices] ITaskServices task,
            [FromRoute] Guid taskId,
            [FromBody] ExtendTaskDueDateRequest request
            )
        {
            var result = await task.ExtendTaskDueDateAsync(mapper.MapExtendDueDate(request, taskId, CurrentUserId));
            return HandleResult(result);
        }

        [EndpointSummary("Change assigned user")]
        [EndpointDescription("Change the assigned user of a specific task.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpPut("{taskId:guid}/change-assigned-user/{assigneeId}")]
        [Authorize(Roles = "Manager")]
        [InvalidateCache(nameof(CacheTags.TaskAll), nameof(CacheTags.AnalyticsAll))]
        public async Task<IActionResult> ChangeAssignedToUserAsync(
            [FromServices] ITaskServices task,
            [FromRoute] Guid taskId,
            [FromRoute] Guid assigneeId
            )
        {
            var result = await task.ChangeAssignedToUserAsync(taskId, assigneeId, CurrentUserId);
            return HandleResult(result);
        }

        [EndpointSummary("Change task status")]
        [EndpointDescription("Change the status of a specific task.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpPut("change-status")]
        [Authorize(Roles = "User,Manager")]
        [InvalidateCache(nameof(CacheTags.TaskAll), nameof(CacheTags.AnalyticsAll), nameof(CacheTags.UserAll))]
        public async Task<IActionResult> ChangeTaskStatusAsync(
            [FromServices] ITaskServices task,
            [FromServices] TaskMapper mapper,
            [FromBody] ChangeTaskStatusRequest request
            )
        {
            var result = await task.ChangeTaskStatusAsync(mapper.MapChangeStatus(request, CurrentUserId));
            return HandleResult(result);
        }

        [EndpointSummary("Add tasks")]
        [EndpointDescription("Add task.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status201Created)]
        [HttpPost]
        [Authorize(Roles = "User,Manager")]
        [InvalidateCache(
            nameof(CacheTags.TaskAll),
            CacheTags.UserDetails,
            nameof(CacheTags.AnalyticsAll))]
        public async Task<IActionResult> AddTaskAsync(
           [FromServices] ITaskServices task,
           [FromServices] TaskMapper mapper,
           [FromBody] AddTaskRequest request
           )
        {
            var result = await task.AddTaskAsync(mapper.MapAddTask(request), CurrentUserId);
            return HandleResult(result);
        }
    }
}
