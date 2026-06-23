using Api.Controllers.Base;
using Domain.Common;
using DTO.Request;
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
            var result = await _sales.GetUserSales(
                CurrentUserId, 
                pagged, 
                sorting, 
                search,
                filter
                );

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
    }
}
