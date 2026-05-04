using Api.Controllers.Base;
using DTO.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
