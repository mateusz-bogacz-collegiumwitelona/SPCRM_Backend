using Api.Controllers.Base;
using Api.Mappers;
using Api.Request.List;
using Api.Request.Unit;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace Api.Controllers
{
    [Route("api/unit")]
    [ApiController]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
    public class UnitController : AuthControllerBase
    {
        [EndpointSummary("Get simple list of unit")]
        [EndpointDescription("Get list of unit without serach, paggination etc.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet("simple")]
        [Authorize]
        public async Task<IActionResult> GetSimpleUnitList([FromServices] IUnitServices unit)
        {
            var result = await unit.GetSimpleUnitList();
            return HandleResult(result);
        }

        [EndpointSummary("Get unit of mesure list with pagination, sorting and filtering")]
        [EndpointDescription("Get unit of mesure list with pagination, sorting and filtering.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet]
        [Authorize(Roles = "Admin")]
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
        [HttpPost]
        [Authorize(Roles = "Admin")]
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
        [HttpPut]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EditUnitAsync(
            [FromServices] IUnitServices unit,
            [FromServices] UnitMapper mapper,
            [FromBody] EditUnitReqeust request)
        {
            var result = await unit.EditUnitAsync(mapper.MapEdit(request));
            return HandleResult(result);
        }
    }
}
