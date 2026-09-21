namespace Services.Response.Analytics
{
    public record AnalyticsChartMetricResponse
    {
        public required string Label { get; init; }
        public List<CurrencyAmountResponse> Revenue { get; init; } = new();
        public required int DealsWonCount { get; init; }
    }
}
