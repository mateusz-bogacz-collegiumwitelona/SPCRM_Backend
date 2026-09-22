using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Controllers.Base
{
    public abstract class BaseControlle : ControllerBase
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
                    500,
                    new
                    {
                        success = false,
                        message = "Result cannot be null."
                    }
                );
            }

            if (result.IsSuccess)
            {
                return StatusCode(result.StatusCode, new
                {
                    success = result.IsSuccess,
                    message = result.Message,
                    data = result.Data
                });
            }

            return StatusCode(result.StatusCode, new
            {
                success = result.IsSuccess,
                message = result.Message,
                errorCode = result.ErrorCode,
                errors = result.Errors
            });
        }

        protected IActionResult HandleResult(Result result)
        {
            if (result == null)
            {
                return StatusCode(
                    500,
                    new
                    {
                        success = false,
                        message = "Result cannot be null."
                    }
                );
            }

            if (result.IsSuccess)
            {
                return StatusCode(result.StatusCode, new
                {
                    success = result.IsSuccess,
                    message = result.Message
                });
            }

            return StatusCode(result.StatusCode, new
            {
                success = result.IsSuccess,
                message = result.Message,
                errorCode = result.ErrorCode,
                errors = result.Errors
            });
        }
    }
}
