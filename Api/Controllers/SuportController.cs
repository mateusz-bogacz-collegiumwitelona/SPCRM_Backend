using Api.Controllers.Base;
using Domain.Common;
using DTO.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace Api.Controllers
{
    [Route("api/suport")]
    [ApiController]
    [AllowAnonymous]
    public class SuportController : BaseController
    {
        private readonly ISupportServices _supportServices;

        public SuportController(ISupportServices supportServices)
        {
            _supportServices = supportServices;
        }

        [EndpointSummary("Send email to support")]
        [EndpointDescription("Sends an email to the support team with the provided details.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
        [HttpPost]
        public async Task<IActionResult> SendEmailToSupport(
            [FromBody] SupportEmailRequest request
            )
        {
            var result = await _supportServices.SendEmailToSupport(request);

            return HandleResult(result);
        }
    }
}
