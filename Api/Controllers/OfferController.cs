using Api.Controllers.Base;
using Api.Mappers;
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
    }
}
