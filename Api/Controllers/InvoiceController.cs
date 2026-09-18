using Api.Controllers.Base;
using Api.Mappers;
using Api.Request.Invoice;
using Api.Request.List;
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

        [EndpointSummary("Get invoice detail")]
        [EndpointDescription("Get invoice detail by invoice id")]
        [HttpGet("{invoiceId}")]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> GetInvoiceDetailAsync(
            [FromServices] IInvoiceService invoice,
            [FromRoute] Guid invoiceId
            )
        {
            var result = await invoice.GetInvoiceDetailAsync(invoiceId);
            return HandleResult(result);
        }


        [EndpointSummary("Get invoice products")]
        [EndpointDescription("Get invoice products by invoice id. This list has paggination and search")]
        [HttpGet("{invoiceId}/products")]
        public async Task<IActionResult> GetInvoiceProductAsync(
            [FromServices] IInvoiceService invoice,
            [FromServices] ApiMapper mapper,
            [FromRoute] Guid invoiceId,
            [FromQuery] SimpleListRequest request
            )
        {
            var result = await invoice.GetInvoiceProductAsync(invoiceId, mapper.MapSimpleList(request));
            return HandleResult(result);
        }

        [EndpointSummary("Get invoice payment summary")]
        [EndpointDescription("Get invoice payment summary by invoice id")]
        [HttpGet("{invoiceId}/payment/summary")]
        public async Task<IActionResult> GetInvoicePaymentSummaryAsync(
            [FromServices] IInvoiceService invoice,
            [FromRoute] Guid invoiceId
            )
        {
            var result = await invoice.GetInvoicePaymentSummaryAsync(invoiceId);
            return HandleResult(result);
        }
    }
}
