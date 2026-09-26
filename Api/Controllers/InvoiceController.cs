using Api.Attributes;
using Api.Controllers.Base;
using Api.Mappers;
using Api.Request.Invoice;
using Api.Request.List;
using Domain.Common;
using Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using Services.Interfaces;
using Services.Response.Invoice;
using Services.Response.Pdf;

namespace Api.Controllers
{
    [Route("api/invoice")]
    [ApiController]
    public class InvoiceController : BaseControlle
    {
        [EndpointSummary("Get invoice list")]
        [EndpointDescription("Get invoice list with pagination, sorting, filtering and search")]
        [ProducesResponseType(typeof(Result<PagedResult<InvoiceResponse>>), StatusCodes.Status200OK)]
        [HttpGet]
        [Authorize(Roles = "User,Manager")]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.InvoicesList })]

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
        [ProducesResponseType(typeof(Result<InvoiceDetailResponse>), StatusCodes.Status200OK)]
        [HttpGet("{invoiceId:guid}")]
        [Authorize(Roles = "User,Manager")]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.InvoiceDetails })]
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
        [ProducesResponseType(typeof(Result<PagedResult<InvoiceProductsListResponse>>), StatusCodes.Status200OK)]
        [HttpGet("{invoiceId:guid}/products")]
        [Authorize(Roles = "User,Manager")]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.InvoiceProducts })]
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
        [ProducesResponseType(typeof(Result<InvoicePaymentSummaryResponse>), StatusCodes.Status200OK)]
        [HttpGet("{invoiceId:guid}/payment/summary")]
        [Authorize(Roles = "User,Manager")]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.InvoicePaymentSummary })]
        public async Task<IActionResult> GetInvoicePaymentSummaryAsync(
            [FromServices] IInvoiceService invoice,
            [FromRoute] Guid invoiceId
            )
        {
            var result = await invoice.GetInvoicePaymentSummaryAsync(invoiceId);
            return HandleResult(result);
        }

        [EndpointSummary("Get invoice payments list")]
        [EndpointDescription("Get invoice payment list witch search")]
        [HttpGet("{invoiceId:guid}/payment")]
        [ProducesResponseType(typeof(Result<PagedResult<InvoicePaymentListResponse>>), StatusCodes.Status200OK)]
        [Authorize(Roles = "User,Manager")]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.InvoicePayments })]
        public async Task<IActionResult> GetInvoicePaymentsAsync(
            [FromServices] IInvoiceService invoice,
            [FromServices] ApiMapper mapper,
            [FromRoute] Guid invoiceId,
            [FromQuery] SimpleListRequest request
            )
        {
            var result = await invoice.GetInvoicePaymentsAsync(invoiceId, mapper.MapSimpleList(request));
            return HandleResult(result);
        }

        [EndpointSummary("Add invoice payment")]
        [EndpointDescription("Add invoice payment")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status201Created)]
        [HttpPost("{invoiceId:guid}/payment")]
        [Authorize(Roles = "User,Manager")]
        [InvalidateCache(nameof(CacheTags.InvoiceAll), CacheTags.DealDetails)]
        public async Task<IActionResult> AddInvoicePaymentAsync(
            [FromServices] IInvoiceService invoice,
            [FromServices] InvoiceMapper mapper,
            [FromRoute] Guid invoiceId,
            [FromBody] AddInvoicePaymentRequest request
            )
        {
            var result = await invoice.AddInvoicePaymentAsync(invoiceId, CurrentUserId, mapper.MapAddPayment(request));
            return HandleResult(result);
        }

        [EndpointSummary("Diownload invoice")]
        [EndpointDescription("Dowloand invoice in pl or en")]
        [ProducesResponseType(typeof(Result<PdfFileResponse>), StatusCodes.Status200OK)]
        [HttpGet("{invoiceId:guid}/pdf")]
        [Authorize(Roles = "User,Manager")]
        [EnableRateLimiting("expensive")]
        public async Task<IActionResult> DownloadInvoicePdf(
            [FromServices] IInvoiceService invoice,
            [FromRoute] Guid invoiceId,
            [FromQuery] string language = "pl"
            )
        {
            var result = await invoice.DownloadInvoicePdfAsync(invoiceId, language);
            return HandleResult(result);
        }
    }
}
