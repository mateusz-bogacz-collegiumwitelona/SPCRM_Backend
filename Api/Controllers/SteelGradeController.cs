using Api.Attributes;
using Api.Controllers.Base;
using Api.Mappers;
using Api.Request.List;
using Api.Request.SteelGrade;
using Domain.Common;
using Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Services.Interfaces;
using Services.Response.Product;
using Services.Response.SteelGrade;

namespace Api.Controllers
{
    [Route("api/steel-grade")]
    [ApiController]
    public class SteelGradeController : BaseControlle
    {
        [EndpointSummary("Get steel grade list")]
        [EndpointDescription("Get steel grade list with pagination, sorting and search.")]
        [ProducesResponseType(typeof(Result<PagedResult<SteelGradeListResponse>>), StatusCodes.Status200OK)]
        [HttpGet]
        [Authorize(Roles = "Admin")]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.SteelGradeList })]
        public async Task<IActionResult> GetSteelGradeListAsync(
            [FromServices] ISteelGradeServices steelGrade,
            [FromServices] ApiMapper mapper,
            [FromQuery] BasicListRequest request
            )
        {
            var result = await steelGrade.GetSteelGradeListAsync(mapper.MapList(request));
            return HandleResult(result);
        }

        [EndpointSummary("Get associated products for a steel grade")]
        [EndpointDescription("Get a list of products associated with a specific steel grade.")]
        [ProducesResponseType(typeof(Result<List<ProductSimpleResponse>>), StatusCodes.Status200OK)]
        [HttpGet("{steelGradeId:guid}/products")]
        [Authorize(Roles = "Admin")]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.SteelGradeAssociatedProducts })]
        public async Task<IActionResult> GetAssociatedProductsAsync(
            [FromServices] ISteelGradeServices steelGradeServices,
            [FromRoute] Guid steelGradeId)
        {
            var result = await steelGradeServices.GetAssociatedProductsAsync(steelGradeId);
            return HandleResult(result);
        }

        [EndpointSummary("Delete a steel grade")]
        [EndpointDescription("Delete a steel grade and update related products.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpDelete("{steelGradeId:guid}")]
        [Authorize(Roles = "Admin")]
        [InvalidateCache(
            nameof(CacheTags.SteelGradeAll),
            nameof(CacheTags.ProductAll),
            nameof(CacheTags.PromotionAll),
            nameof(CacheTags.AnalyticsAll)
            )]
        public async Task<IActionResult> DeleteSteelGradeAsync(
            [FromServices] ISteelGradeServices steelGradeServices,
            [FromServices] SteelGradeMapper mapper,
            [FromRoute] Guid steelGradeId,
            [FromBody] DeleteSteelGradeRequest? request)
        {
            var result = await steelGradeServices.DeleteSteelGradeAsync(
                steelGradeId,
                mapper.MapReassignments(request?.Reassignments)
                );
            return HandleResult(result);
        }

        [EndpointSummary("Edit a steel grade")]
        [EndpointDescription("Edit the details of an existing steel grade.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result), StatusCodes.Status409Conflict)]
        [HttpPatch]
        [Authorize(Roles = "Admin")]
        [InvalidateCache(nameof(CacheTags.SteelGradeAll), nameof(CacheTags.ProductAll), nameof(CacheTags.PromotionAll))]
        public async Task<IActionResult> EditSteelGradeAsync(
            [FromServices] ISteelGradeServices steelGradeServices,
            [FromServices] SteelGradeMapper mapper,
            [FromBody] EditSteelGradeRequest request)
        {
            var result = await steelGradeServices.EditSteelGradeAsync(mapper.MapEdit(request));
            return HandleResult(result);
        }

        [EndpointSummary("Create a new steel grade")]
        [EndpointDescription("Create a new steel grade with the specified details.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(Result), StatusCodes.Status409Conflict)]
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [InvalidateCache(nameof(CacheTags.SteelGradeAll),
            nameof(CacheTags.ProductAll), 
            nameof(CacheTags.PromotionAll),
            nameof(CacheTags.AnalyticsAll)
            )]
        public async Task<IActionResult> AddSteelGradeAsync(
            [FromServices] ISteelGradeServices steelGradeServices,
            [FromServices] SteelGradeMapper mapper,
            [FromBody] AddSteelGradeRequest request)
        {
            var result = await steelGradeServices.AddSteelGradeAsync(mapper.MapAdd(request));
            return HandleResult(result);
        }
    }
}
