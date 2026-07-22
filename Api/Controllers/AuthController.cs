using Api.Controllers.Base;
using Api.Mappers;
using Api.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    [Tags("Authentication")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public class AuthController : AuthControllerBase
    {
        [EndpointSummary("Authenticate user (Login Step 1)")]
        [EndpointDescription("Authenticates a user using their email and password. " +
            "If credentials are valid, a HttpOnlyCookie was given.")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> LoginAsync(
            [FromBody] LoginRequest request, 
            [FromServices] AuthMapper mapper,
            [FromServices] IAuthServices authServices
            )
        {
            var statusCode = await authServices.LoginAsync(mapper.MapLoginAsync(request));

            return statusCode == StatusCodes.Status401Unauthorized
                ? Unauthorized()
                : NoContent();
        }

        
        [EndpointSummary("Logout user (Login Step 2)")]
        [EndpointDescription("Logs out the authenticated user by clearing the authentication cookie.")]
        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> LogoutAsync(
            [FromServices] IAuthServices authServices
            )
        {
            var statusCode = await authServices.LogoutAsync();
            return NoContent();
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetUserDataAsync(
            [FromServices] IAuthServices authServices
            )
        {
            var result = await authServices.GetUserDataAsync(CurrentUserId);
            return HandleResult(result);
        }
    }
}
