using Api.Attributes;
using Api.Controllers.Base;
using Api.Mappers;
using Api.Request.Promotion;
using Domain.Common;
using Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Services.Interfaces;
using Services.Response.Promotion;

namespace Api.Controllers
{
    [Route("api/promotion")]
    [ApiController]
    public class PromotionController : BaseControlle
    {
        [EndpointSummary("Get promotion list")]
        [EndpointDescription("Get promotion list with pagination, sorting and filtering.")]
        [ProducesResponseType(typeof(Result<PagedResult<PromotionResponse>>), StatusCodes.Status200OK)]
        [HttpGet]
        [Authorize(Roles = "Manager,User")]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.PromotionsList })]

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
        [ProducesResponseType(typeof(Result<PromotionDetailResponse>), StatusCodes.Status200OK)]
        [HttpGet("{promotionId:guid}")]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.PromotionDetails })]
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
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpPatch("{promotionId:guid}/deactivate")]
        [Authorize(Roles = "Manager")]
        [InvalidateCache(nameof(CacheTags.PromotionAll), nameof(CacheTags.ProductAll))]
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
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result), StatusCodes.Status409Conflict)]
        [HttpPatch("activate")]
        [Authorize(Roles = "Manager")]
        [InvalidateCache(nameof(CacheTags.PromotionAll), nameof(CacheTags.ProductAll))]
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
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpDelete("{promotionId:guid}")]
        [Authorize(Roles = "Manager")]
        [InvalidateCache(nameof(CacheTags.PromotionAll), nameof(CacheTags.ProductAll))]
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
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result), StatusCodes.Status409Conflict)]
        [HttpPatch("edit")]
        [Authorize(Roles = "Manager")]
        [InvalidateCache(nameof(CacheTags.PromotionAll), nameof(CacheTags.ProductAll))]
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
        [ProducesResponseType(typeof(Result), StatusCodes.Status409Conflict)]
        [HttpPost]
        [InvalidateCache(nameof(CacheTags.PromotionAll), nameof(CacheTags.ProductAll))]
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
