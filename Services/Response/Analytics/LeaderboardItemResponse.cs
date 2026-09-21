namespace Services.Response.Analytics
{
    public record LeaderboardItemResponse
    {
        public Guid EmployeeId { get; init; }
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public required string Email { get; init; }
        public decimal RevenueThisMonth { get; init; }
        public int WonDealsThisMonth { get; init; }
        public int ActiveDealsCount { get; init; }
        public decimal WinRatePercentageThisMonth { get; init; }
    }
}
