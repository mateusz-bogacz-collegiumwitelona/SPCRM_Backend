using Api.Controllers.Base;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace Api.Controllers
{
    [Route("api/currency")]
    [ApiController]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
    public class CurrencyController : AuthControllerBase
    {
        [EndpointSummary("Get currency list")]
        [EndpointDescription("Get simple currency list with no pagination, sorting and filtering.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet("simple")]
        [Authorize]
        public async Task<IActionResult> GetCurrencySimpleListAsync([FromServices] ICurrencyServices currency)
        {
            var result = await currency.GetCurrencySimpleListAsync();
            return HandleResult(result);
        }
    }
}
