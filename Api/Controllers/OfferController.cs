using Api.Controllers.Base;
using Api.Mappers;
using Api.Request.List;
using Api.Request.Offer;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetTopologySuite.IO;
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
    }
}
