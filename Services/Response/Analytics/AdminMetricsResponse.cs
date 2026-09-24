namespace Services.Response.Analytics
{
    public record AdminMetricsResponse
    {
        public int TotalUsers { get; init; }
        public int TotalSteelGrades { get; init; }
        public int TotalCurrencies { get; init; }
        public int TotalUnits { get; init; }
    }
}
