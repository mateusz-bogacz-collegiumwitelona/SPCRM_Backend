using Api.Controllers.Base;
using Api.Mappers;
using Api.Request;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace Api.Controllers
{
    [Route("api/mailing")]
    [ApiController]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
    public class MailingController : AuthControllerBase
    {
        [EndpointSummary("Send email to support")]
        [EndpointDescription("Sends an email to the support team with the provided details.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpPost("support")]
        [AllowAnonymous]
        public async Task<IActionResult> SendEmailToSupport(
            [FromServices] MailingMapper mapper,
            [FromServices] IMailingServices _supportServices,
            [FromBody] SupportEmailRequest request
            )
        {
            var result = await _supportServices.SendEmailToSupport(mapper.MapEmail(request));
            return HandleResult(result);
        }

        [HttpPost("mailing")]
        [Authorize(Roles = "User,Manager")]

        public async Task<IActionResult> SendProductMailingAsync(
            [FromServices] IMailingServices mailing,
            [FromServices] MailingMapper mapper,
            [FromBody] MailingRequest request
            )
        {
            var result = await mailing.SendProductMailingAsync(mapper.MapProductMailing(request), CurrentUserId);
            return HandleResult(result);
        }
    }
}
