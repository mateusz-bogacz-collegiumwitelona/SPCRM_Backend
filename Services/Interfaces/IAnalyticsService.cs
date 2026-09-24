using Domain.Common;
using Services.Command.Analytics;
using Services.Command.List;
using Services.Response.Analytics;
using Services.Response.Pdf;

namespace Services.Interfaces
{
    public interface IAnalyticsService
    {
        Task<Result<TeamKpiSummaryResponse>> GetTeamKpiSummaryAsync();
        Task<Result<List<AnalyticsChartMetricResponse>>> GetTeamRevenueChartAsync(AnalyticsChartCommand command);
        Task<Result<EmployeeKpiSummaryResponse>> GetEmployeeKpiSummaryAsync(Guid employeeId);
        Task<Result<List<AnalyticsChartMetricResponse>>> GetEmployeeRevenueChartAsync(Guid employeeId, AnalyticsChartCommand command);
        Task<Result<PagedResult<LeaderboardItemResponse>>> GetTeamLeaderboardAsync(PaggedCommand command);
        Task<Result<PdfFileResponse>> GenerateEmployeeReportPdfAsync(Guid employeeId, AnalyticsChartCommand chartCommand);
        Task<Result<PdfFileResponse>> GenerateTeamReportPdfAsync(AnalyticsChartCommand chartCommand);
        Task<Result<AdminMetricsResponse>> GetAdminMetricsAsync();
    }
}
