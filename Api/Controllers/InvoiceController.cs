using Api.Controllers.Base;
using Api.Mappers;
using Api.Request.Invoice;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace Api.Controllers
{
    [Route("api/invoice")]
    [ApiController]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
    public class InvoiceController : AuthControllerBase
    {
        [EndpointSummary("Get invoice list")]
        [EndpointDescription("Get invoice list with pagination, sorting, filtering and search")]
        [HttpGet]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> GetInvoiceListAsync(
            [FromServices] IInvoiceService invoice,
            [FromServices] InvoiceMapper mapper,
            [FromQuery] InvoiceListRequest request
            )
        {
            var result = await invoice.GetInvoiceListAsync(mapper.MapList(request));
            return HandleResult(result);
        }
    }
}
