using Api.Controllers.Base;
using Api.Mappers;
using Api.Request;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace Api.Controllers
{
    [Route("api/promotion")]
    [ApiController]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
    public class PromotionController : AuthControllerBase
    {
        [EndpointSummary("Get promotion list")]
        [EndpointDescription("Get promotion list with pagination, sorting and filtering.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet("")]
        [Authorize(Roles = "Manager,User")]
        public async Task<IActionResult> GetPromotionListAsync(
            [FromServices] IPromotionServices promotion,
            [FromServices] PromotionMapper mapper,
            [FromQuery] PromotionListRequest request
            )
        {
            var result = await promotion.GetPromotionListAsync(mapper.MapList(request));
            return HandleResult(result);
        }
    }
}
