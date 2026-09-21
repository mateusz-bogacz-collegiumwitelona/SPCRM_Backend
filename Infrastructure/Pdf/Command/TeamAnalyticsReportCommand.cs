namespace Infrastructure.Pdf.Command
{
    public record TeamAnalyticsReportCommand
    {
        public  List<CurrencyAmountCommand> RevenueThisWeek { get; init; } = new();
        public List<CurrencyAmountCommand> RevenueThisMonth { get; init; } = new();
        public List<CurrencyAmountCommand> RevenueThisYear { get; init; } = new();
        public required int ActiveDealsCount { get; init; }
        public required int WonDealsThisMonth { get; init; }
        public required int LostDealsThisMonth { get; init; }
        public required int CompletedTasksThisMonth { get; init; }
        public required int OverdueTasksCount { get; init; }

        public required string PeriodTitle { get; init; }
        public DateTime GeneratedAtUtc { get; init; } = DateTime.UtcNow;

        public List<HistoryMetricCommand> HistoryMetrics { get; init; } = new();
        public List<TeamLeaderboardRowCommand> TopPerformers { get; init; } = new();
    }
}
