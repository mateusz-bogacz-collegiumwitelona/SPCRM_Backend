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
    }
}
