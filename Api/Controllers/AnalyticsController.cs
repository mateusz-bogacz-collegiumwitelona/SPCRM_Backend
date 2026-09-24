using Api.Controllers.Base;
using Api.Mappers;
using Api.Request.Analytics;
using Api.Request.List;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Services.Interfaces;
using Services.Response.Analytics;
using Services.Response.Pdf;

namespace Api.Controllers
{
    [Route("api/analytics")]
    [ApiController]
    public class AnalyticsController : BaseControlle
    {
        [EndpointSummary("Get team KPI summary")]
        [EndpointDescription("Returns overall financial, deals, and task performance metrics for the whole team.")]
        [ProducesResponseType(typeof(Result<TeamKpiSummaryResponse>), StatusCodes.Status200OK)]
        [HttpGet("team/kpi")]
        [Authorize(Roles = "Manager")]

        public async Task<IActionResult> GetTeamKpiSummaryAsync([FromServices] IAnalyticsService analytics)
        {
            var result = await analytics.GetTeamKpiSummaryAsync();
            return HandleResult(result);
        }

        [EndpointSummary("Get team revenue chart data")]
        [EndpointDescription("Returns aggregated revenue and deal counts for charts.")]
        [ProducesResponseType(typeof(Result<List<AnalyticsChartMetricResponse>>), StatusCodes.Status200OK)]
        [HttpGet("team/chart")]
        [Authorize(Roles = "Manager")]
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
        [ProducesResponseType(typeof(Result<List<AnalyticsChartMetricResponse>>), StatusCodes.Status200OK)]
        [HttpGet("employees/{employeeId:guid}/chart")]
        [Authorize(Roles = "Manager")]
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
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> GetTeamLeaderboardAsync(
            [FromServices] IAnalyticsService analytics,
            [FromServices] ApiMapper mapper,
            [FromQuery] PaggedRequest request)
        {
            var result = await analytics.GetTeamLeaderboardAsync(mapper.MapPagged(request));
            return HandleResult(result);
        }


        [EndpointSummary("Get employee KPI summary")]
        [EndpointDescription("Returns overall financial, deals, conversion rate, and task performance metrics for a specific employee.")]
        [ProducesResponseType(typeof(Result<EmployeeKpiSummaryResponse>), StatusCodes.Status200OK)]
        [HttpGet("employees/{employeeId:guid}/kpi")]
        [Authorize(Roles = "Manager")]

        public async Task<IActionResult> GetEmployeeKpiSummaryAsync(
            [FromRoute] Guid employeeId,
            [FromServices] IAnalyticsService analytics)
        {
            var result = await analytics.GetEmployeeKpiSummaryAsync(employeeId);
            return HandleResult(result);
        }

        [EndpointSummary("Download employee analytics report PDF")]
        [EndpointDescription("Generates and returns an analytical PDF performance report for a specific employee.")]
        [ProducesResponseType(typeof(Result<PdfFileResponse>), StatusCodes.Status200OK)]
        [HttpGet("employees/{employeeId:guid}/report/pdf")]
        [Authorize(Roles = "Manager")]
        [EnableRateLimiting("expensive")]
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
        [ProducesResponseType(typeof(Result<PdfFileResponse>), StatusCodes.Status200OK)]
        [HttpGet("team/report/pdf")]
        [Authorize(Roles = "Manager")]
        [EnableRateLimiting("expensive")]
        public async Task<IActionResult> DownloadTeamReportPdfAsync(
            [FromServices] IAnalyticsService analytics,
            [FromServices] AnalyticsMapper mapper,
            [FromQuery] AnalyticsChartRequest request)
        {
            var result = await analytics.GenerateTeamReportPdfAsync(mapper.MapChart(request));
            return HandleResult(result);
        }


        [EndpointSummary("Get employee KPI summary")]
        [EndpointDescription("Returns overall financial, deals, conversion rate, and task performance metrics for a specific employee.")]
        [ProducesResponseType(typeof(Result<EmployeeKpiSummaryResponse>), StatusCodes.Status200OK)]
        [HttpGet("me/kpi")]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> GetMeKpiSummaryAsync([FromServices] IAnalyticsService analytics)
        {
            var result = await analytics.GetEmployeeKpiSummaryAsync(CurrentUserId);
            return HandleResult(result);
        }

        [EndpointSummary("Get system metrics for admin overview")]
        [ProducesResponseType(typeof(Result<AdminMetricsResponse>), StatusCodes.Status200OK)]
        [HttpGet("admin/metrics")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAdminMetricsAsync([FromServices] IAnalyticsService analytics)
        {
            var result = await analytics.GetAdminMetricsAsync();
            return HandleResult(result);
        }
    }
}
