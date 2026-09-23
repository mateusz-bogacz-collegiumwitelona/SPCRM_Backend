namespace Api.Request.Analytics
{
    public record AnalyticsChartRequest
    {
        public required string Period { get; init; }
        public Guid? CurrencyId { get; init; }
    }
}
