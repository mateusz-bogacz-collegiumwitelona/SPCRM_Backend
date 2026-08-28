using Api.Controllers.Base;
using Api.Mappers;
using Api.Request.Promotion;
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

        [EndpointSummary("Get promotion detail")]
        [EndpointDescription("Get detailed information about a specific promotion.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet("{promotionId:guid}")]
        [Authorize(Roles = "Manager,User")]
        public async Task<IActionResult> GetPromotionDetailAsync(
            [FromServices] IPromotionServices promotion,
            [FromRoute] Guid promotionId
        )
        {
            var result = await promotion.GetPromotionDetailAsync(promotionId);
            return HandleResult(result);
        }

        [EndpointSummary("Deactivate promotion")]
        [EndpointDescription("Deactivates an active promotion and sets its end date to now.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpPatch("{promotionId:guid}/deactivate")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> DeactivatePromotionAsync(
            [FromServices] IPromotionServices promotionServices,
            [FromRoute] Guid promotionId
        )
        {
            var result = await promotionServices.DeactivatePromotionAsync(promotionId);
            return HandleResult(result);
        }

        [EndpointSummary("Activate promotion")]
        [EndpointDescription("Activates an inactive promotion and sets its start date to now.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpPatch("activate")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> ActivatePromotionAsync(
            [FromServices] IPromotionServices promotionServices,
            [FromServices] PromotionMapper mapper,
            [FromBody] ActivatePromotionRequest request
            )
        {
            var result = await promotionServices.ActivatePromotionAsync(mapper.MapActivate(request));
            return HandleResult(result);
        }

        [EndpointSummary("Delete promotion (Soft delete)")]
        [EndpointDescription("Soft deletes a promotion.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpDelete("{promotionId:guid}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> DeletePromotionAsync(
            [FromServices] IPromotionServices promotionServices,
            [FromRoute] Guid promotionId
        )
        {
            var result = await promotionServices.DeletePromotionAsync(promotionId);
            return HandleResult(result);
        }

        [EndpointSummary("Edit promotion")]
        [EndpointDescription("Edits an existing promotion.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpPatch("edit")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> EditPromotionAsync(
            [FromServices] IPromotionServices promotionServices,
            [FromServices] PromotionMapper mapper,
            [FromBody] EditPromotionRequest request
        )
        {
            var result = await promotionServices.EditPromotionAsync(mapper.MapEdit(request));
            return HandleResult(result);
        }

        [EndpointSummary("Create promotion")]
        [EndpointDescription("Creates a new active promotion for a specific product.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status201Created)]
        [HttpPost]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> AddPromotionAsync(
            [FromServices] IPromotionServices promotionServices,
            [FromServices] PromotionMapper mapper,
            [FromBody] AddPromotionRequest request
        )
        {
            var result = await promotionServices.AddPromotionAsync(mapper.MapAdd(request));
            return HandleResult(result);
        }
    }
}
