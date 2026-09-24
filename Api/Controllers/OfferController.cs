using Api.Attributes;
using Api.Controllers.Base;
using Api.Mappers;
using Api.Request.List;
using Api.Request.Offer;
using Domain.Common;
using Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using Services.Interfaces;
using Services.Response.Offer;

namespace Api.Controllers
{
    [Route("api/offer")]
    [ApiController]
    [Authorize(Roles = "Manager,User")]
    public class OfferController : BaseControlle
    {
        [EndpointSummary("Get offer list")]
        [EndpointDescription("Get offer list with pagination, sorting and filtering.")]
        [ProducesResponseType(typeof(Result<PagedResult<OfferListResponse>>), StatusCodes.Status200OK)]
        [HttpGet]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.OffersList })]
        public async Task<IActionResult> GetOfferListAsync(
            [FromServices] IOfferServices offer,
            [FromServices] OfferMapper mapper,
            [FromQuery] OfferListRequest request
            )
        {
            var result = await offer.GetOfferListAsync(mapper.MapList(request));
            return HandleResult(result);
        }

        [EndpointSummary("Get offer detail")]
        [EndpointDescription("Get offer detail by ID.")]
        [ProducesResponseType(typeof(Result<OfferDetailResponse>), StatusCodes.Status200OK)]
        [HttpGet("detail/{offerId:guid}")]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.OfferDetails })]
        public async Task<IActionResult> GetOfferDetailAsync(
            [FromServices] IOfferServices offer,
            [FromRoute] Guid offerId
            )
        {
            var result = await offer.GetOfferDetailAsync(offerId);
            return HandleResult(result);
        }

        [EndpointSummary("Get offer client detail")]
        [EndpointDescription("Get offer client detail by offer ID.")]
        [ProducesResponseType(typeof(Result<OfferClientDetail>), StatusCodes.Status200OK)]
        [HttpGet("client/{offerId:guid}")]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.OfferClientDetails })]
        public async Task<IActionResult> GetOfferClientDetailAsync(
            [FromServices] IOfferServices offer,
            [FromRoute] Guid offerId
            )
        {
            var result = await offer.GetOfferClientDetailAsync(offerId);
            return HandleResult(result);
        }

        [EndpointSummary("Get offer product detail")]
        [EndpointDescription("Get offer product detail by offer ID. " +
            "This list have search and paggination.")]
        [ProducesResponseType(typeof(Result<PagedResult<OfferProductResponse>>), StatusCodes.Status200OK)]
        [HttpGet("product/{offerId:guid}")]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.OfferProducts })]
        public async Task<IActionResult> GetOfferProductsAsync(
            [FromServices] IOfferServices offer,
            [FromServices] ApiMapper mapper,
            [FromRoute] Guid offerId,
            [FromQuery] SimpleListRequest request
            )
        {
            var result = await offer.GetOfferProductsAsync(offerId, mapper.MapSimpleList(request));
            return HandleResult(result);
        }

        [EndpointSummary("Extend offer validity")]
        [EndpointDescription("Extend offer validity by offer ID. User can get data but not must.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpPatch("extend")]
        [InvalidateCache(CacheTags.OffersList, CacheTags.OfferDetails, CacheTags.OfferAllowedActions)]
        public async Task<IActionResult> ExtendOfferValidityAsync(
            [FromServices] IOfferServices offer,
            [FromServices] OfferMapper mapper,
            [FromBody] ExtendOfferValidityRequest request
            )
        {
            var result = await offer.ExtendOfferValidityAsync(mapper.MapExtend(request));
            return HandleResult(result);
        }



        [EndpointSummary("Change offer status")]
        [EndpointDescription("Change offer status by offer ID")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpPatch("change-status")]
        [EnableRateLimiting("expensive")]
        [InvalidateCache(CacheTags.OffersList, CacheTags.OfferDetails, CacheTags.OfferAllowedActions)]
        public async Task<IActionResult> ChangeOfferStatusAsync(
            [FromServices] IOfferServices offer,
            [FromServices] OfferMapper mapper,
            [FromBody] ChangeOfferStatusRequest request)
        {
            var result = await offer.ChangeOfferStatusAsync(mapper.MapChangeStatus(request));
            return HandleResult(result);
        }

        [EndpointSummary("Update offer products")]
        [EndpointDescription("Update offer products by offer ID.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpPut("products")]
        [InvalidateCache(nameof(CacheTags.OffersAll), nameof(CacheTags.ProductAll))]
        public async Task<IActionResult> UpdateOfferProductsAsync(
            [FromServices] IOfferServices offer,
            [FromServices] OfferMapper mapper,
            [FromBody] UpdateOfferProductsRequest request)
        {
            var result = await offer.UpdateOfferProductsAsync(mapper.MapUpdateProducts(request));
            return HandleResult(result);
        }

        [EndpointSummary("Resend offer email")]
        [EndpointDescription("Resend offer email by offer ID.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpPost("resend-email")]
        [EnableRateLimiting("expensive")]
        public async Task<IActionResult> ResendOfferEmailAsync(
            [FromServices] IOfferServices offer,
            [FromServices] OfferMapper mapper,
            [FromBody] ResendOfferEmailRequest request
            )
        {
            var result = await offer.ResendOfferEmailAsync(mapper.MapResendEmail(request));
            return HandleResult(result);
        }

        [EndpointSummary("Delete offer")]
        [EndpointDescription("Delete offer by offer ID.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpDelete("{offerId:guid}")]
        [InvalidateCache(nameof(CacheTags.OffersAll), nameof(CacheTags.ProductAll))]
        public async Task<IActionResult> DeleteOfferAsync(
            [FromServices] IOfferServices offer,
            [FromRoute] Guid offerId
            )
        {
            var result = await offer.DeleteOfferAsync(offerId);
            return HandleResult(result);
        }

        [EndpointSummary("Get offer allowed actions")]
        [EndpointDescription("Get offer allowed actions by offer ID.")]
        [ProducesResponseType(typeof(Result<OfferAllowedActionsResponse>), StatusCodes.Status200OK)]
        [HttpGet("{id:guid}/allowed-actions")]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.OfferAllowedActions })]
        public async Task<IActionResult> GetOfferAllowedActionsAsync(
            [FromServices] IOfferServices offerServices,
            [FromRoute] Guid id)
        {
            var result = await offerServices.GetOfferAllowedActionsAsync(id);
            return HandleResult(result);
        }

        [EndpointSummary("Get offer status list")]
        [EndpointDescription("Get offer status list.")]
        [ProducesResponseType(typeof(Result<List<string>>), StatusCodes.Status200OK)]
        [HttpGet("status")]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.OfferStatuses })]
        public async Task<IActionResult> GetOfferStatusAsync([FromServices] IOfferServices offerServices)
        {
            var result = await offerServices.GetOfferStatus();
            return HandleResult(result);
        }
    }
}
