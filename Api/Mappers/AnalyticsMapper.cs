using Api.Request.Analytics;
using Domain.Enum;
using Riok.Mapperly.Abstractions;
using Services.Command.Analytics;

namespace Api.Mappers
{
    [Mapper]
    public partial class AnalyticsMapper
    {
        [MapProperty(nameof(AnalyticsChartRequest.Period), nameof(AnalyticsChartCommand.Period), Use = nameof(ParsePeriod))]
        public partial AnalyticsChartCommand MapChart(AnalyticsChartRequest request);

        private AnalyticsPeriodEnum ParsePeriod(string period)
            => Enum.TryParse<AnalyticsPeriodEnum>(period.Trim(), ignoreCase: true, out var parsed)
                ? parsed
                : throw new ArgumentException($"Invalid period value: {period}");
    }
}
