namespace Infrastructure.Pdf.Command
{
    public record TeamLeaderboardRowCommand
    {
        public required string FullName { get; init; }
        public List<CurrencyAmountCommand> RevenueThisMonth { get; init; } = new();
        public required int WonDealsThisMonth { get; init; }
        public required decimal WinRatePercentageThisMonth { get; init; }
    }
}
