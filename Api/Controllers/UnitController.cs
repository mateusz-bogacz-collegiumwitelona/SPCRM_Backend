using Api.Attributes;
using Api.Controllers.Base;
using Api.Mappers;
using Api.Request.List;
using Api.Request.Unit;
using Domain.Common;
using Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Services.Interfaces;
using Services.Response.Unit;

namespace Api.Controllers
{
    [Route("api/unit")]
    [ApiController]
    public class UnitController : BaseControlle
    {
        [EndpointSummary("Get simple list of unit")]
        [EndpointDescription("Get list of unit without serach, paggination etc.")]
        [ProducesResponseType(typeof(Result<List<UnitSimpleListResponse>>), StatusCodes.Status200OK)]
        [HttpGet("simple")]
        [Authorize]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.UnitSimple })]
        public async Task<IActionResult> GetSimpleUnitList([FromServices] IUnitServices unit)
        {
            var result = await unit.GetSimpleUnitList();
            return HandleResult(result);
        }

        [EndpointSummary("Get unit of mesure list with pagination, sorting and filtering")]
        [EndpointDescription("Get unit of mesure list with pagination, sorting and filtering.")]
        [ProducesResponseType(typeof(Result<PagedResult<UnitListResponse>>), StatusCodes.Status200OK)]
        [HttpGet]
        [Authorize(Roles = "Admin")]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.UnitList })]
        public async Task<IActionResult> GetUnitListAsync(
            [FromServices] IUnitServices unit,
            [FromServices] ApiMapper mapper,
            [FromQuery] BasicListRequest request)
        {
            var result = await unit.GetUnitListAsync(mapper.MapList(request));
            return HandleResult(result);
        }

        [EndpointSummary("Add new unit of mesure")]
        [EndpointDescription("Add new unit of mesure.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(Result), StatusCodes.Status409Conflict)]
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [InvalidateCache(nameof(CacheTags.UnitAll), nameof(CacheTags.AnalyticsAll))]
        public async Task<IActionResult> AddUnitAsync(
            [FromServices] IUnitServices unit,
            [FromServices] UnitMapper mapper,
            [FromBody] AddUnitRequest request)
        {
            var result = await unit.AddUnitAsync(mapper.MapAdd(request));
            return HandleResult(result);
        }

        [EndpointSummary("Edit existing unit of mesure")]
        [EndpointDescription("Edit existing unit of mesure.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(Result), StatusCodes.Status409Conflict)]
        [HttpPut]
        [Authorize(Roles = "Admin")]
        [InvalidateCache(
            nameof(CacheTags.UnitAll),
            nameof(CacheTags.ProductAll),
            nameof(CacheTags.PromotionAll),
            nameof(CacheTags.OffersAll))]
        public async Task<IActionResult> EditUnitAsync(
            [FromServices] IUnitServices unit,
            [FromServices] UnitMapper mapper,
            [FromBody] EditUnitRequest request)
        {
            var result = await unit.EditUnitAsync(mapper.MapEdit(request));
            return HandleResult(result);
        }
    }
}
