using Api.Controllers.Base;
using Domain.Common;
using DTO.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace Api.Controllers
{
    [Authorize]
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
        public async Task<IActionResult> GetUserSales(
            [FromQuery] PaggedRequest pagged,
            [FromQuery] CompanyFilterRequest filter)
        {
            var result = await _sales.GetUserSales(CurrentUserId, pagged, filter);

            return HandleResult(result);
        }
    }
}
