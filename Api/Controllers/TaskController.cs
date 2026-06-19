using Api.Controllers.Base;
using Domain.Common;
using DTO.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace Api.Controllers
{
    [Route("api/tasks")]
    [ApiController]
    public class TaskController : AuthControllerBase
    {
        private readonly ITaskServices _taskServices;

        public TaskController(ITaskServices taskServices)
        {
            _taskServices = taskServices;
        }


        [EndpointSummary("Get tasks for calendar")]
        [EndpointDescription("Show tasks for calendar view. This endpoint return tasks within a specified date range")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
        [HttpGet("calendar")]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> GetTasksForCalendarAsync([FromQuery]TaskCalendarRequest request)
        {
            var result = await _taskServices.GetTasksForCalendarAsync(CurrentUserId, request);
            return HandleResult(result);
        }

        [EndpointSummary("Get task dictionaries")]
        [EndpointDescription("Returns available statuses and priorities for frontend dropdowns.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
        [HttpGet("dictionaries")]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> GetTaskDictionariesAsync()
        {
            var result = await _taskServices.GetTaskDictionariesAsync();
            return HandleResult(result);
        }

        [EndpointSummary("Get task detail")]
        [EndpointDescription("Returns detailed information about a specific task.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
        [HttpGet("{taskId}")]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> GetTaskDetailResponse([FromRoute] Guid taskId)
        {
            var result = await _taskServices.GetTaskDetailResponse(taskId);
            return HandleResult(result);
        }

        [EndpointSummary("Get task contact")]
        [EndpointDescription("Returns contact information associated with a specific task.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
        [HttpGet("{taskId}/contact")]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> GetTaskContactAsync([FromRoute]Guid taskId)
        {
            var result = await _taskServices.GetTaskContactAsync(taskId);
            return HandleResult(result);
        }

    }
}
