using Domain.Common;
using Services.Response.Analytics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Services.Interfaces
{
    public interface IAnalyticsService
    {
        Task<Result<TeamKpiSummaryResponse>> GetTeamKpiSummaryAsync();
    }
}
