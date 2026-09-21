using Domain.Enum;
using Infrastructure.Pdf.Command;
using Services.Response.Analytics;

namespace Services.Helpers
{
    public class AnalyticsMapper
    {
        public EmployeeAnalyticsReportCommand MapToReportModel(
            EmployeeKpiSummaryResponse summary,
            List<AnalyticsChartMetricResponse> historyMetrics,
            AnalyticsPeriodEnum period)
        {
            var periodTitle = period switch
            {
                AnalyticsPeriodEnum.HalfYear => "Historia Sprzedaży (Ostatnie 6 miesięcy)",
                AnalyticsPeriodEnum.CurrentMonth => "Historia Sprzedaży (Bieżący miesiąc)",
                _ => "Historia Sprzedaży (Bieżący rok)"
            };

            return new EmployeeAnalyticsReportCommand
            {
                FirstName = summary.FirstName,
                LastName = summary.LastName,
                Email = summary.Email,
                RevenueThisWeek = summary.RevenueThisWeek,
                RevenueThisMonth = summary.RevenueThisMonth,
                RevenueThisYear = summary.RevenueThisYear,
                WinRatePercentageThisMonth = summary.WinRatePercentageThisMonth,
                ActiveDealsPipelineValue = summary.ActiveDealsPipelineValue,
                CompletedTasksThisMonth = summary.CompletedTasksThisMonth,
                OverdueTasksCount = summary.OverdueTasksCount,
                PeriodTitle = periodTitle,
                HistoryMetrics = historyMetrics.Select(h => new EmployeeHistoryMetricCommand
                {
                    Label = h.Label,
                    Revenue = h.Revenue,
                    DealsWonCount = h.DealsWonCount
                }).ToList()
            };
        }
    }
}
