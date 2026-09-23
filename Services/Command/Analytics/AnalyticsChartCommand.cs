using Domain.Enum;

namespace Services.Command.Analytics
{
    public record AnalyticsChartCommand
    {
        public required AnalyticsPeriodEnum Period { get; init; }
        public Guid? CurrencyId { get; init; }
    }
}
