using Api.Controllers.Base;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace Api.Controllers
{
    [Route("api/analytics")]
    [ApiController]
    [Authorize(Roles = "Manager")]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
    public class AnalyticsController : AuthControllerBase
    {
        [EndpointSummary("Get team KPI summary")]
        [EndpointDescription("Returns overall financial, deals, and task performance metrics for the whole team.")]
        [HttpGet("team/kpi")]
        public async Task<IActionResult> GetTeamKpiSummaryAsync([FromServices] IAnalyticsService analytics)
        {
            var result = await analytics.GetTeamKpiSummaryAsync();
            return HandleResult(result);
        }
    }
}
