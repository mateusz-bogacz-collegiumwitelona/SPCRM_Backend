using Api.Controllers.Base;
using Api.Mappers;
using Api.Request.Task;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace Api.Controllers
{
    [Route("api/tasks")]
    [ApiController]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
    public class TaskController : AuthControllerBase
    {
        [EndpointSummary("Get tasks for calendar")]
        [EndpointDescription("Show tasks for calendar view. " +
            "This endpoint return tasks within a specified date range")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet("calendar")]
        [Authorize(Roles = "User,Manager")]
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
        public async Task<IActionResult> GetTaskDictionariesAsync([FromServices] ITaskServices taskServices)
        {
            var result = await taskServices.GetTaskDictionariesAsync();
            return HandleResult(result);
        }

        [EndpointSummary("Get task detail")]
        [EndpointDescription("Returns detailed information about a specific task.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet("{taskId}")]
        [Authorize(Roles = "User,Manager")]
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
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet("{taskId}/contact")]
        [Authorize(Roles = "User,Manager")]
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
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet("{taskId}/deal")]
        [Authorize(Roles = "User,Manager")]
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
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet("{taskId}/notes")]
        [Authorize(Roles = "User,Manager")]
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
        [HttpDelete("{taskId}")]
        [Authorize(Roles = "User,Manager")]
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
        [HttpPut("{taskId}")]
        [Authorize(Roles = "User,Manager")]
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
        [HttpPut("{taskId}/extend-due-date")]
        [Authorize(Roles = "User,Manager")]
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
        [HttpPut("{taskId}/change-assigned-user/{assigneeId}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> ChangeAssignedToUserAsync(
            [FromServices] ITaskServices task,
            [FromRoute] Guid taskId,
            [FromRoute] Guid assigneeId
            )
        {
            var result = await task.ChangeAssignedToUserAsync(taskId, assigneeId, CurrentUserId);
            return HandleResult(result);
        }
    }
}
