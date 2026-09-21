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
                RevenueThisWeek = MapCurrencies(summary.RevenueThisWeek),
                RevenueThisMonth = MapCurrencies(summary.RevenueThisMonth),
                RevenueThisYear = MapCurrencies(summary.RevenueThisYear),
                WinRatePercentageThisMonth = summary.WinRatePercentageThisMonth,
                ActiveDealsPipelineValue = MapCurrencies(summary.ActiveDealsPipelineValue),
                CompletedTasksThisMonth = summary.CompletedTasksThisMonth,
                OverdueTasksCount = summary.OverdueTasksCount,
                PeriodTitle = periodTitle,
                HistoryMetrics = historyMetrics.Select(h => new HistoryMetricCommand
                {
                    Label = h.Label,
                    Revenue = MapCurrencies(h.Revenue),
                    DealsWonCount = h.DealsWonCount
                }).ToList()
            };
        }

        public TeamAnalyticsReportCommand MapToTeamReportModel(
            TeamKpiSummaryResponse summary,
            List<AnalyticsChartMetricResponse> historyMetrics,
            List<LeaderboardItemResponse> topPerformers,
            AnalyticsPeriodEnum period)
        {
            var periodTitle = period switch
            {
                AnalyticsPeriodEnum.HalfYear => "Historia Sprzedaży (Ostatnie 6 miesięcy)",
                AnalyticsPeriodEnum.CurrentMonth => "Historia Sprzedaży (Bieżący miesiąc)",
                _ => "Historia Sprzedaży (Bieżący rok)"
            };

            return new TeamAnalyticsReportCommand
            {
                RevenueThisWeek = MapCurrencies(summary.RevenueThisWeek),
                RevenueThisMonth = MapCurrencies(summary.RevenueThisMonth),
                RevenueThisYear = MapCurrencies(summary.RevenueThisYear),
                ActiveDealsCount = summary.ActiveDealsCount,
                WonDealsThisMonth = summary.WonDealsThisMonth,
                LostDealsThisMonth = summary.LostDealsThisMonth,
                CompletedTasksThisMonth = summary.CompletedTasksThisMonth,
                OverdueTasksCount = summary.OverdueTasksCount,
                PeriodTitle = periodTitle,
                HistoryMetrics = historyMetrics.Select(h => new HistoryMetricCommand
                {
                    Label = h.Label,
                    Revenue = MapCurrencies(h.Revenue),
                    DealsWonCount = h.DealsWonCount
                }).ToList(),
                TopPerformers = topPerformers.Select(p => new TeamLeaderboardRowCommand
                {
                    FullName = $"{p.FirstName} {p.LastName}".Trim(),
                    RevenueThisMonth = MapCurrencies(p.RevenueThisMonth),
                    WonDealsThisMonth = p.WonDealsThisMonth,
                    WinRatePercentageThisMonth = p.WinRatePercentageThisMonth
                }).ToList()
            };
        }

        private static List<CurrencyAmountCommand> MapCurrencies(IEnumerable<CurrencyAmountResponse>? source)
                => source?.Select(c => new CurrencyAmountCommand
                {
                    CurrencyCode = c.CurrencyCode,
                    Amount = c.Amount,
                    DecimalPlaces = c.DecimalPlaces
                }).ToList() ?? [];
    }
}
