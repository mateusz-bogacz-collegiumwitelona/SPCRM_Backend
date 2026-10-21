using Domain.Common;
using Domain.Constants;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Controllers.Base
{
    [ProducesResponseType(typeof(Result), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status500InternalServerError)]
    public abstract class BaseController : ControllerBase
    {
        protected Guid CurrentUserId
        {
            get
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                return Guid.Parse(userId!);
            }
        }

        protected IActionResult HandleResult<T>(Result<T> result)
        {
            if (result == null)
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    Result<T>.Failure(
                        message: "Result cannot be null.",
                        errorCode: ErrorCodes.InternalError,
                        statusCode: StatusCodes.Status500InternalServerError
                    )
                );
            }

            return StatusCode(result.StatusCode, result);
        }

        protected IActionResult HandleResult(Result result)
        {
            if (result == null)
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    Result.Failure(
                        message: "Result cannot be null.",
                        errorCode: ErrorCodes.InternalError,
                        statusCode: StatusCodes.Status500InternalServerError
                    )
                );
            }

            return StatusCode(result.StatusCode, result);
        }
    }
}
