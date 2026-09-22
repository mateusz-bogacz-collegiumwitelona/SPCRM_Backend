using Api.Controllers.Base;
using Api.Mappers;
using Api.Request.List;
using Api.Request.Mailing;
using Api.Request.Support;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Services.Interfaces;
using Services.Response.Contact;
using Services.Response.Product;

namespace Api.Controllers
{
    [Route("api/mailing")]
    [ApiController]
    public class MailingController : BaseControlle
    {
        [EndpointSummary("Send email to support")]
        [EndpointDescription("Sends an email to the support team with the provided details.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpPost("support")]
        [AllowAnonymous]
        [EnableRateLimiting("auth-strict")]
        public async Task<IActionResult> SendEmailToSupport(
            [FromServices] MailingMapper mapper,
            [FromServices] IMailingServices _supportServices,
            [FromBody] SupportEmailRequest request
            )
        {
            var result = await _supportServices.SendEmailToSupport(mapper.MapEmail(request));
            return HandleResult(result);
        }

        [EndpointSummary("Send product mailing and record offers")]
        [EndpointDescription("Sends promotional product mailing emails to specified clients and automatically creates persistent, " +
            "trackable offer records in the database with their respective quoted prices and expiration details.")]
        [HttpPost("offert")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [Authorize(Roles = "User,Manager")]
        [EnableRateLimiting("expensive")]
        public async Task<IActionResult> SendProductMailingAsync(
            [FromServices] IMailingServices mailing,
            [FromServices] MailingMapper mapper,
            [FromBody] MailingRequest request
            )
        {
            var result = await mailing.SendProductMailingAsync(mapper.MapProductMailing(request), CurrentUserId);
            return HandleResult(result);
        }

        [EndpointSummary("Get client data for mailing")]
        [EndpointDescription("Retrieves a list of client contacts for mailing purposes, with optional search and pagination.")]
        [ProducesResponseType(typeof(Result<PagedResult<MailingClientResponse>>), StatusCodes.Status200OK)]
        [HttpGet("contacts")]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> GetClientDataToMailingAsync(
            [FromServices] IContactServices contact,
            [FromServices] ApiMapper mapper,
            [FromQuery] SimpleListRequest request
            )
        {
            var result = await contact.GetClientDataToMailingAsync(mapper.MapSimpleList(request));
            return HandleResult(result);
        }

        [EndpointSummary("Get product data for mailing")]
        [EndpointDescription("Retrieves a list of products for mailing purposes, with optional search and pagination.")]
        [ProducesResponseType(typeof(Result<PagedResult<MailingProductResponse>>), StatusCodes.Status200OK)]
        [HttpGet("products")]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> GetProductDataToMailingAsync(
            [FromServices] IProductSevices product,
            [FromServices] ApiMapper mapper,
            [FromQuery] SimpleListRequest request
            )
        {
            var result = await product.GetMailingProductsAsync(mapper.MapSimpleList(request));
            return HandleResult(result);
        }
    }
}
