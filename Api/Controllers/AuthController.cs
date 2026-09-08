using Api.Controllers.Base;
using Api.Mappers;
using Api.Request.Auth;
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
            var statusCode = await authServices.LoginAsync(mapper.MapLogin(request));

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

        [EndpointSummary("Request password reset link")]
        [EndpointDescription("Sends a password reset link to the provided email address if the account exists and is confirmed.")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPasswordAsync(
            [FromBody] ForgotPasswordRequest request,
            [FromServices] AuthMapper mapper,
            [FromServices] IUserServices userServices
        )
        {
            var result = await userServices.ForgotPasswordAsync(mapper.MapForgotPassword(request));
            return HandleResult(result);
        }

        [EndpointSummary("Reset password")]
        [EndpointDescription("Resets user password using the token sent via email.")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPasswordAsync(
            [FromBody] ResetPasswordRequest request,
            [FromServices] AuthMapper mapper,
            [FromServices] IUserServices userServices
        )
        {
            var result = await userServices.ResetPasswordAsync(mapper.MapResetPassword(request));
            return HandleResult(result);
        }
    }
}
