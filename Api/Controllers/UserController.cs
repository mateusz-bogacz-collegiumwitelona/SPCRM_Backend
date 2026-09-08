using Api.Controllers.Base;
using Api.Mappers;
using Api.Request.User;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace Api.Controllers
{
    [Route("api/user")]
    [ApiController]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
    public class UserController : AuthControllerBase
    {
        [EndpointSummary("Get simple list of users")]
        [EndpointDescription("Get list of users without serach, paggination etc. And without admins")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet("simple")]
        [Authorize]
        public async Task<IActionResult> GetUserSimpleListAsync(
            [FromServices] IUserServices user
            )
        {
            var result = await user.GetUserSimpleListAsync();
            return HandleResult(result);
        }

        [EndpointSummary("Get list of users")]
        [EndpointDescription("Get list of users with search, paggination etc.")]
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetUserListAsync(
            [FromServices] IUserServices user,
            [FromServices] UserMapper mapper,
            [FromQuery] UserListRequest request
            )
        {
            var result = await user.GetUserListAsync(mapper.MapList(request));
            return HandleResult(result);
        }

        [EndpointSummary("Add new user")]
        [EndpointDescription("Add new user with role")]
        [HttpPost("create")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateUserAsync(
            [FromServices] IUserServices user,
            [FromServices] UserMapper mapper,
            [FromBody] AddUserRequest request
            )
        {
            var result = await user.CreateUserAsync(mapper.MapAdd(request));
            return HandleResult(result);
        }

        [EndpointSummary("Confirm email")]
        [EndpointDescription("Confirm email with token")]
        [HttpPost("confirm-email")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ConfirmEmailAsync(
            [FromServices] IUserServices user,
            [FromServices] UserMapper mapper,
            [FromBody] ConfirmEmailRequest request
            )
        {
            var result = await user.ConfirmEmailAsync(mapper.MapConfirmEmail(request));
            return HandleResult(result);
        }
    }
}
