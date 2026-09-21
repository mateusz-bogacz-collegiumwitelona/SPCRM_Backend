using Api.Controllers.Base;
using Api.Mappers;
using Api.Request.Analytics;
using Api.Request.List;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;
using Services.Response.Analytics;

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

        [EndpointSummary("Get team revenue chart data")]
        [EndpointDescription("Returns aggregated revenue and deal counts for charts.")]
        [HttpGet("team/chart")]
        public async Task<IActionResult> GetTeamRevenueChartAsync(
            [FromServices] IAnalyticsService analytics,
            [FromServices] AnalyticsMapper mapper,
            [FromQuery] AnalyticsChartRequest request
            )
        {
            var result = await analytics.GetTeamRevenueChartAsync(mapper.MapChart(request));
            return HandleResult(result);
        }

        [EndpointSummary("Get employee revenue chart data")]
        [EndpointDescription("Returns aggregated revenue and deal counts for an individual employee charts.")]
        [HttpGet("employees/{employeeId:guid}/chart")]
        public async Task<IActionResult> GetEmployeeRevenueChartAsync(
            [FromRoute] Guid employeeId,
            [FromServices] IAnalyticsService analytics,
            [FromServices] AnalyticsMapper mapper,
            [FromQuery] AnalyticsChartRequest request)
        {
            var result = await analytics.GetEmployeeRevenueChartAsync(employeeId, mapper.MapChart(request));
            return HandleResult(result);
        }

        [EndpointSummary("Get team sales leaderboard")]
        [EndpointDescription("Returns a paginated leaderboard of active sales employees ranked by revenue generated this month.")]
        [ProducesResponseType(typeof(Result<PagedResult<LeaderboardItemResponse>>), StatusCodes.Status200OK)]
        [HttpGet("team/leaderboard")]
        public async Task<IActionResult> GetTeamLeaderboardAsync(
            [FromServices] IAnalyticsService analytics,
            [FromServices] ApiMapper mapper,
            [FromQuery] PaggedRequest request)
        {
            var result = await analytics.GetTeamLeaderboardAsync(mapper.MapPagged(request));
            return HandleResult(result);
        }

        [EndpointSummary("Download employee analytics report PDF")]
        [EndpointDescription("Generates and returns an analytical PDF performance report for a specific employee.")]
        [HttpGet("employees/{employeeId:guid}/report/pdf")]
        public async Task<IActionResult> DownloadEmployeeReportPdfAsync(
            [FromRoute] Guid employeeId,
            [FromServices] IAnalyticsService analytics,
            [FromServices] AnalyticsMapper mapper,
            [FromQuery] AnalyticsChartRequest request)
        {
            var result = await analytics.GenerateEmployeeReportPdfAsync(employeeId, mapper.MapChart(request));
            return HandleResult(result);
        }

        [EndpointSummary("Download team analytics report PDF")]
        [EndpointDescription("Generates and returns an analytical PDF performance report for the entire sales team.")]
        [HttpGet("team/report/pdf")]
        public async Task<IActionResult> DownloadTeamReportPdfAsync(
            [FromServices] IAnalyticsService analytics,
            [FromServices] AnalyticsMapper mapper,
            [FromQuery] AnalyticsChartRequest request)
        {
            var result = await analytics.GenerateTeamReportPdfAsync(mapper.MapChart(request));
            return HandleResult(result);
        }
    }
}
