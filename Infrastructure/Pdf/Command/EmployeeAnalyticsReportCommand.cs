namespace Infrastructure.Pdf.Command
{
    public record EmployeeAnalyticsReportCommand
    {
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public required string Email { get; init; }

        public List<CurrencyAmountCommand> RevenueThisWeek { get; init; } = new();
        public List<CurrencyAmountCommand> RevenueThisMonth { get; init; } = new();
        public List<CurrencyAmountCommand> RevenueThisYear { get; init; } = new();

        public required decimal WinRatePercentageThisMonth { get; init; }
        public  List<CurrencyAmountCommand>  ActiveDealsPipelineValue { get; init; } = new();
        public required int CompletedTasksThisMonth { get; init; }
        public required int OverdueTasksCount { get; init; }
        public required string PeriodTitle { get; init; }
        public DateTime GeneratedAtUtc { get; init; } = DateTime.UtcNow;

        public List<HistoryMetricCommand> HistoryMetrics { get; init; } = new();
    }
}
