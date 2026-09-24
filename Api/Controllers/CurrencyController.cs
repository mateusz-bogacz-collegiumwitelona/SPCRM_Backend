using Api.Attributes;
using Api.Controllers.Base;
using Api.Mappers;
using Api.Request.Currency;
using Api.Request.List;
using Domain.Common;
using Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Services.Interfaces;
using Services.Response.Currency;

namespace Api.Controllers
{
    [Route("api/currency")]
    [ApiController]
    public class CurrencyController : BaseControlle
    {
        [EndpointSummary("Get currency list")]
        [EndpointDescription("Get simple currency list with no pagination, sorting and filtering.")]
        [ProducesResponseType(typeof(Result<List<CurrencyListResponse>>), StatusCodes.Status200OK)]
        [HttpGet("simple")]
        [Authorize]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.CurrencySimple })]
        public async Task<IActionResult> GetCurrencySimpleListAsync([FromServices] ICurrencyServices currency)
        {
            var result = await currency.GetCurrencySimpleListAsync();
            return HandleResult(result);
        }

        [EndpointSummary("Get currency list with pagination, sorting and filtering")]
        [EndpointDescription("Get currency list with pagination, sorting and filtering.")]
        [ProducesResponseType(typeof(Result<PagedResult<CurrencyListResponse>>), StatusCodes.Status200OK)]
        [HttpGet]
        [Authorize(Roles = "Admin")]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.CurrencyList })]
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
        [ProducesResponseType(typeof(Result), StatusCodes.Status201Created)]
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [InvalidateCache(nameof(CacheTags.CurrencyAll), CacheTags.AnalyticsAdminMetrics)]
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
        [ProducesResponseType(typeof(Result), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [InvalidateCache(
            nameof(CacheTags.CurrencyAll),
            nameof(CacheTags.ProductAll),
            nameof(CacheTags.DealAll),
            nameof(CacheTags.InvoiceAll),
            nameof(CacheTags.AnalyticsAll))]
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
