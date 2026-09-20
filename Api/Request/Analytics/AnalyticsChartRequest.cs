namespace Api.Request.Analytics
{
    public record AnalyticsChartRequest
    {
        public required string Period { get; init; }
    }
}
