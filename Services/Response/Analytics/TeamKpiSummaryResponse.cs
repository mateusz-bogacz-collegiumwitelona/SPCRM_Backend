namespace Services.Response.Analytics
{
    public record TeamKpiSummaryResponse
    {
        public List<CurrencyAmountResponse> RevenueThisWeek { get; init; } = new();
        public List<CurrencyAmountResponse> RevenueThisMonth { get; init; } = new();
        public List<CurrencyAmountResponse> RevenueThisYear { get; init; } = new();

        public int ActiveDealsCount { get; init; }
        public int WonDealsThisMonth { get; init; }
        public int LostDealsThisMonth { get; init; }

        public int CompletedTasksThisMonth { get; init; }
        public int PendingTasksCount { get; init; }
        public int OverdueTasksCount { get; init; }
    }
}
