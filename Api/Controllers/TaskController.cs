using Api.Controllers.Base;
using Api.Mappers;
using Api.Request;
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
            [FromServices] ITaskServices taskServices,
            [FromRoute] Guid taskId
        )
        {
            var result = await taskServices.GetTaskNotesAsync(taskId);
            return HandleResult(result);
        }
    }
}
