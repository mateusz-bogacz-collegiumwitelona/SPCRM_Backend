using Api.Controllers.Base;
using Api.Mappers;
using Api.Request;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace Api.Controllers
{
    [Route("api/sales")]
    [ApiController]
    public class SalesController : AuthControllerBase
    {
        private readonly ISalesServices _sales;

        public SalesController(ISalesServices sales)
        {
            _sales = sales;
        }

        [EndpointSummary("Get user deals")]
        [EndpointDescription("Show data of every user deals.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
        [HttpGet("")]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> GetUserSales(
            [FromQuery] PaggedRequest pagged,
            [FromQuery] SortingRequest sorting,
            [FromQuery] SearchRequest search,
            [FromQuery] SalesFilterRequest filter)
        {
            var mapper = new SalesMapper();
            var result = await _sales.GetUserSales(CurrentUserId, mapper.MapList(pagged, sorting, search, filter));

            return HandleResult(result);
        }

        [EndpointSummary("Get sales statuses")]
        [EndpointDescription("Show available sales statuses.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
        [HttpGet("statuses")]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> GetSalesStatuses()
        {
            var result = await _sales.GetSalesStatus();
            return HandleResult(result);
        }

        [EndpointSummary("Get sale detail")]
        [EndpointDescription("Returns detailed information about a specific sale.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
        [HttpGet("{dealId}")]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> GetSaleDetailAsync([FromRoute] Guid dealId)
        {
            var result = await _sales.GetSaleDetailAsync(dealId);
            return HandleResult(result);
        }

        [EndpointSummary("Get deal products")]
        [EndpointDescription("Returns a paginated list of products associated with a specific deal.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
        [HttpGet("{dealId}/products")]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> GetDealProductAsync(
            [FromRoute] Guid dealId,
            [FromQuery] PaggedRequest pagged,
            [FromQuery] SortingRequest sorting,
            [FromQuery] SearchRequest search,
            [FromQuery] ProductFilterRequest filter)
        {
            var mapper = new ProductMapper();
            var result = await _sales.GetDealProductAsync(dealId, mapper.MapList(pagged, sorting, search, filter));
            return HandleResult(result);
        }

        [EndpointSummary("Get deal notes")]
        [EndpointDescription("Returns a list of notes associated with a specific deal.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
        [HttpGet("{dealId}/notes")]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> GetDealNotesAsync([FromRoute] Guid dealId)
        {
            var result = await _sales.GetDealNotesAsync(dealId);
            return HandleResult(result);
        }
    }
}

