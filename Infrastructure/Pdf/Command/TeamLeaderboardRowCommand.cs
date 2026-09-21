namespace Infrastructure.Pdf.Command
{
    public record TeamLeaderboardRowCommand
    {
        public required string FullName { get; init; }
        public required decimal RevenueThisMonth { get; init; }
        public required int WonDealsThisMonth { get; init; }
        public required decimal WinRatePercentageThisMonth { get; init; }
    }
}
