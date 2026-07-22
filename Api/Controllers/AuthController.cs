using Api.Controllers.Base;
using Api.Mappers;
using Api.Request;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;
using Services.Response;

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
            "If credentials are valid, a final JWT token is returned.")]
        [ProducesResponseType(typeof(Result<AuthResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
        [HttpPost("login")]
        public async Task<IActionResult> LoginAsync(
            [FromBody] LoginRequest request, 
            [FromServices] AuthMapper mapper,
            [FromServices] IAuthServices auth
            )
        {
            var result = await auth.LoginAsync(mapper.MapLoginAsync(request));
            return HandleResult(result);
        }
    }
}
