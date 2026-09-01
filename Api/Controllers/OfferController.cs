using Api.Controllers.Base;
using Api.Mappers;
using Api.Request.List;
using Api.Request.Offer;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace Api.Controllers
{
    [Route("api/offer")]
    [ApiController]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
    public class OfferController : AuthControllerBase
    {
        [EndpointSummary("Get offer list")]
        [EndpointDescription("Get offer list with pagination, sorting and filtering.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet]
        [Authorize]
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
        [HttpGet("detail/{offerId:guid}")]
        [Authorize]
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
        [HttpGet("client/{offerId:guid}")]
        [Authorize]
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
        [HttpGet("product/{offerId:guid}")]
        [Authorize]
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
        [Authorize]
        [HttpPatch("extend")]
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
        [HttpPatch("status")]
        [Authorize]
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
        [HttpPut("products")]
        [Authorize]
        public async Task<IActionResult> UpdateOfferProductsAsync(
            [FromServices] IOfferServices offerServices,
            [FromServices] OfferMapper mapper,
            [FromBody] UpdateOfferProductsRequest request)
        {
            var command = mapper.MapUpdateProducts(request);
            var result = await offerServices.UpdateOfferProductsAsync(command);
            return HandleResult(result);
        }
    }
}
