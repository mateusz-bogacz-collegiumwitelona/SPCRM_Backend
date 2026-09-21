using Domain.Common;
using Services.Command.Analytics;
using Services.Response.Analytics;

namespace Services.Interfaces
{
    public interface IAnalyticsService
    {
        Task<Result<TeamKpiSummaryResponse>> GetTeamKpiSummaryAsync();
        Task<Result<List<AnalyticsChartMetricResponse>>> GetTeamRevenueChartAsync(AnalyticsChartCommand command);
        Task<Result<EmployeeKpiSummaryResponse>> GetEmployeeKpiSummaryAsync(Guid employeeId);
    }
}
