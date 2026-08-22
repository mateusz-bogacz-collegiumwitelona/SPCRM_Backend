using Api.Controllers.Base;
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

    }
}
