namespace Services.Response.Analytics
{
    public record EmployeeKpiSummaryResponse
    {
        public Guid EmployeeId { get; init; }
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public required string Email { get; init; }
        public decimal RevenueThisWeek { get; init; }
        public decimal RevenueThisMonth { get; init; }
        public decimal RevenueThisYear { get; init; }
        public int ActiveDealsCount { get; init; }
        public decimal ActiveDealsPipelineValue { get; init; }
        public int WonDealsThisMonth { get; init; }
        public int LostDealsThisMonth { get; init; }
        public decimal WinRatePercentageThisMonth { get; init; }
        public int CompletedTasksThisMonth { get; init; }
        public int PendingTasksCount { get; init; }
        public int OverdueTasksCount { get; init; }
    }
}
