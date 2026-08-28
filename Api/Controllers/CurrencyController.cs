using Api.Controllers.Base;
using Api.Mappers;
using Api.Request;
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

        [EndpointSummary("Get currency list with pagination, sorting and filtering")]
        [EndpointDescription("Get currency list with pagination, sorting and filtering.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetCurrenyListAsync(
            [FromServices] ICurrencyServices currency,
            [FromServices] ApiMapper mapper,
            [FromQuery] BasicListRequest request)
        {
            var result = await currency.GetCurrenyListAsync(mapper.MapList(request));
            return HandleResult(result);
        }

        [EndpointSummary("Add currency")]
        [EndpointDescription("Add a new currency.")]
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddCurrencyAsync(
            [FromServices] ICurrencyServices currency,
            [FromServices] CurrencyMapper mapper,
            [FromBody] AddCurrencyRequest request)
        {
            var result = await currency.AddCurrencyAsync(mapper.MapAdd(request));
            return HandleResult(result);
        }

        [EndpointSummary("Edit currency")]
        [EndpointDescription("Edit currency by id.")]
        [HttpPatch]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EditCurrencyAsync(
            [FromServices] ICurrencyServices currency,
            [FromServices] CurrencyMapper mapper,
            [FromBody] EditCurrencyRequest request)
        {
            var result = await currency.EditCurrencyAsync(mapper.MapEdit(request));
            return HandleResult(result);
        }
    }
}
