namespace Services.Response.Analytics
{
    public record TeamKpiSummaryResponse
    {
        public decimal RevenueThisWeek { get; init; }
        public decimal RevenueThisMonth { get; init; }
        public decimal RevenueThisYear { get; init; }

        public int ActiveDealsCount { get; init; }
        public int WonDealsThisMonth { get; init; }
        public int LostDealsThisMonth { get; init; }

        public int CompletedTasksThisMonth { get; init; }
        public int PendingTasksCount { get; init; }
        public int OverdueTasksCount { get; init; }
    }
}
