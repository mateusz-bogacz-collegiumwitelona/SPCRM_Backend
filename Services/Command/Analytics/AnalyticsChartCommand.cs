using Domain.Enum;

namespace Services.Command.Analytics
{
    public record AnalyticsChartCommand
    {
        public required AnalyticsPeriodEnum Period { get; init; }
    }
}
