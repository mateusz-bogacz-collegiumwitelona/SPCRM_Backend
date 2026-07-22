using Api.Controllers.Base;
using Api.Mappers;
using Api.Request;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;
using Services.Response;
using Services.Services;

namespace Api.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("api/auth")]
    [Tags("Authentication")]
    public class AuthController : BaseController
    {
        [EndpointSummary("Authenticate user (Login Step 1)")]
        [EndpointDescription("Authenticates a user using their email and password. " +
            "If credentials are valid, a HttpOnlyCookie was given.")]
        [ProducesResponseType(typeof(Result<AuthResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
        [HttpPost("login")]
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
    }
}
