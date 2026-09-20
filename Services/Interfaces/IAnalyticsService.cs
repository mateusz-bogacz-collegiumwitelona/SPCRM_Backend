using Domain.Common;
using Services.Command.Analytics;
using Services.Response.Analytics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Services.Interfaces
{
    public interface IAnalyticsService
    {
        Task<Result<TeamKpiSummaryResponse>> GetTeamKpiSummaryAsync();
        Task<Result<List<AnalyticsChartMetricResponse>>> GetTeamRevenueChartAsync(AnalyticsChartCommand command);
    }
}
