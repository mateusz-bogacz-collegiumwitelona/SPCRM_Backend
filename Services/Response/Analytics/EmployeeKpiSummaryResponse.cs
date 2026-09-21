namespace Services.Response.Analytics
{
    public record EmployeeKpiSummaryResponse
    {
        public Guid EmployeeId { get; init; }
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public required string Email { get; init; }
        public List<CurrencyAmountResponse> RevenueThisWeek { get; init; } = new();
        public List<CurrencyAmountResponse> RevenueThisMonth { get; init; } = new();
        public List<CurrencyAmountResponse> RevenueThisYear { get; init; } = new();
        public int ActiveDealsCount { get; init; }
        public List<CurrencyAmountResponse> ActiveDealsPipelineValue { get; init; } = new();
        public int WonDealsThisMonth { get; init; }
        public int LostDealsThisMonth { get; init; }
        public decimal WinRatePercentageThisMonth { get; init; }
        public int CompletedTasksThisMonth { get; init; }
        public int PendingTasksCount { get; init; }
        public int OverdueTasksCount { get; init; }
    }
}
