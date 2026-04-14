using Domain.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Base
{
    [ApiController]
    [Route("api/[controller]")]
    public abstract class BaseController : ControllerBase
    {
        protected IActionResult HandleResult<T>(Result<T> result)
        {
            if (result == null)
            {
                return StatusCode(
                    500, 
                    new { 
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
                errors = result.Errors
            });
        }
    }
}
