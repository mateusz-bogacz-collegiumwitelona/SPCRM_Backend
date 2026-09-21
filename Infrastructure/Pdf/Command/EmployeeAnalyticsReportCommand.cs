namespace Infrastructure.Pdf.Command
{
    public record EmployeeAnalyticsReportCommand
    {
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public required string Email { get; init; }

        public required decimal RevenueThisWeek { get; init; }
        public required decimal RevenueThisMonth { get; init; }
        public required decimal RevenueThisYear { get; init; }

        public required decimal WinRatePercentageThisMonth { get; init; }
        public required decimal ActiveDealsPipelineValue { get; init; }
        public required int CompletedTasksThisMonth { get; init; }
        public required int OverdueTasksCount { get; init; }
        public required string PeriodTitle { get; init; }
        public DateTime GeneratedAtUtc { get; init; } = DateTime.UtcNow;

        public List<EmployeeHistoryMetricCommand> HistoryMetrics { get; init; } = new();
    }
}
