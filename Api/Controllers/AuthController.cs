using Api.Controllers.Base;
using Domain.Common;
using DTO.Request;
using DTO.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace Api.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("api/auth")]
    [Tags("Authentication")]
    public class AuthController : BaseController
    {
        private readonly IAuthServices _auth;

        public AuthController(IAuthServices auth)
        {
            _auth = auth;
        }

        
        [EndpointSummary("Authenticate user (Login Step 1)")]
        [EndpointDescription("Authenticates a user using their email and password. " +
            "If credentials are valid, a final JWT token is returned.")]
        [ProducesResponseType(typeof(Result<AuthResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
        [HttpPost("login")]
        public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request)
        {
            var result = await _auth.LoginAsync(request);
            return HandleResult(result);
        }
    }
}
