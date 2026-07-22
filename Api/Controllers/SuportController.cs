using Api.Controllers.Base;
using Api.Mappers;
using Api.Request;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace Api.Controllers
{
    [Route("api/support")]
    [ApiController]
    [AllowAnonymous]
    public class SuportController : BaseController
    {
        [EndpointSummary("Send email to support")]
        [EndpointDescription("Sends an email to the support team with the provided details.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
        [HttpPost]
        public async Task<IActionResult> SendEmailToSupport(
            [FromServices] SupportMapper mapper,
            [FromServices] ISupportServices _supportServices,
            [FromBody] SupportEmailRequest request
            )
        {
            var result = await _supportServices.SendEmailToSupport(mapper.MapEmail(request));
            return HandleResult(result);
        }
    }
}
